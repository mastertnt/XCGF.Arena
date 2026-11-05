using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Evaluates the sensor equipments at each step and writes what each one sees into its <see cref="SensorDetections"/>.
/// </summary>
/// <remarks>
/// A sensor is an equipment whose type has a registered <see cref="ISensorModel"/>. The built-in models follow the
/// sensor definitions of the XCGF.Data standard library: <see cref="SensorModel"/> (Sensor), <see cref="RadarModel"/>
/// (Radar), <see cref="EsmModel"/> (ESM), <see cref="InfraredModel"/> (Infrared) and <see cref="VisualModel"/> (Visual).
/// The candidate targets are all the entities with <see cref="EntityIdentity"/> and <see cref="Kinematic"/>, except
/// the carrier of the sensor. A sensor scans again once its ScanInterval has elapsed since its last scan (every step
/// when unset) and keeps its MaxContacts nearest detections.
/// </remarks>
public sealed class SensorSystem : QuerySystem<Equipment, EquipmentData>
{
    private readonly Dictionary<string, ISensorModel> mModels = new Dictionary<string, ISensorModel>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SensorSystem"/> class, with the built-in models.
    /// </summary>
    public SensorSystem()
    {
        RegisterModel(SensorModel.SensorType, new SensorModel());
        RegisterModel(RadarModel.SensorType, new RadarModel());
        RegisterModel(EsmModel.SensorType, new EsmModel());
        RegisterModel(InfraredModel.SensorType, new InfraredModel());
        RegisterModel(VisualModel.SensorType, new VisualModel());
    }

    /// <summary>
    /// Gets the detection models, by equipment type.
    /// </summary>
    public IReadOnlyDictionary<string, ISensorModel> Models => mModels;

    /// <summary>
    /// Registers the detection model of an equipment type, replacing any previous one.
    /// </summary>
    /// <param name="pType">The equipment type, as declared by the definition, e.g. Radar.</param>
    /// <param name="pModel">The model.</param>
    public void RegisterModel(string pType, ISensorModel pModel) => mModels[pType] = pModel ?? throw new ArgumentNullException(nameof(pModel));

    /// <summary>
    /// Computes the geometry of a target as seen from a carrier.
    /// </summary>
    /// <param name="pTarget">The target entity.</param>
    /// <param name="pCarrier">Kinematic state of the carrier.</param>
    /// <param name="pTargetKinematic">Kinematic state of the target.</param>
    /// <returns>The geometry.</returns>
    public static SensorGeometry ComputeGeometry(Entity pTarget, in Kinematic pCarrier, in Kinematic pTargetKinematic)
    {
        double lHorizontal = GeoMath.Distance(pCarrier.Position, pTargetKinematic.Position);
        double lVertical = pTargetKinematic.Position.Altitude - pCarrier.Position.Altitude;
        double lBearing = GeoMath.Bearing(pCarrier.Position, pTargetKinematic.Position);
        double lRelative = GeoMath.NormalizeHeading(lBearing - pCarrier.Orientation.Heading + 180) - 180;
        double lElevation = Math.Atan2(lVertical, lHorizontal) * 180 / Math.PI;
        double lRange = Math.Sqrt((lHorizontal * lHorizontal) + (lVertical * lVertical));
        return new SensorGeometry(pTarget, pCarrier, pTargetKinematic, lRange, lBearing, lRelative, lElevation);
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        TimeSpan lNow = TimeSpan.FromSeconds(Tick.time);
        Entity[] lTargets = Query.Store.Query<EntityIdentity, Kinematic>().Entities.ToArray();

        // Snapshot the sensors: adding SensorDetections is a structural change.
        foreach (Entity lSensor in Query.Entities.ToArray())
        {
            Equipment lEquipment = lSensor.GetComponent<Equipment>();
            if (lEquipment.Type is null || !mModels.TryGetValue(lEquipment.Type, out ISensorModel? lModel))
            {
                continue;
            }

            EquipmentData lData = lSensor.GetComponent<EquipmentData>();
            if (lSensor.TryGetComponent(out SensorDetections lPrevious)
                && lData.Technological.GetNumber("ScanInterval") is { } lInterval
                && (lNow - lPrevious.Time).TotalSeconds < lInterval - 1e-9)
            {
                continue;
            }

            Entity lCarrier = lSensor.Parent;
            List<Detection> lDetections = new List<Detection>();
            if (!lCarrier.IsNull && lCarrier.TryGetComponent(out Kinematic lCarrierKinematic) && lModel.IsActive(lData))
            {
                foreach (Entity lTarget in lTargets)
                {
                    if (lTarget == lCarrier)
                    {
                        continue;
                    }

                    SensorGeometry lGeometry = ComputeGeometry(lTarget, lCarrierKinematic, lTarget.GetComponent<Kinematic>());
                    if (lModel.Detects(lData, lGeometry))
                    {
                        lDetections.Add(new Detection(lTarget, lGeometry.Range, lGeometry.Bearing, lGeometry.RelativeBearing));
                    }
                }
            }

            if (lData.Technological.GetNumber("MaxContacts") is { } lMax && lDetections.Count > lMax)
            {
                lDetections.Sort((pLeft, pRight) => pLeft.Range.CompareTo(pRight.Range));
                lDetections.RemoveRange((int)Math.Max(0, lMax), lDetections.Count - (int)Math.Max(0, lMax));
            }

            SensorDetections lResult = new SensorDetections(lDetections, lNow);
            if (lSensor.HasComponent<SensorDetections>())
            {
                lSensor.GetComponent<SensorDetections>() = lResult;
            }
            else
            {
                lSensor.AddComponent(lResult);
            }
        }
    }
}
