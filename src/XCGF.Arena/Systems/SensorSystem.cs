using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Evaluates the sensor equipments at each step and writes what each one sees into its <see cref="SensorDetections"/>.
/// </summary>
/// <remarks>
/// A sensor is an equipment whose type has a registered <see cref="ISensorModel"/>; <see cref="RadarModel"/> is
/// registered for the Radar type. The candidate targets are all the entities with <see cref="EntityIdentity"/> and
/// <see cref="Kinematic"/>, except the carrier of the sensor.
/// </remarks>
public sealed class SensorSystem : QuerySystem<Equipment, EquipmentData>
{
    private readonly Dictionary<string, ISensorModel> mModels = new Dictionary<string, ISensorModel>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SensorSystem"/> class, with the built-in models.
    /// </summary>
    public SensorSystem()
    {
        RegisterModel(RadarModel.SensorType, new RadarModel());
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
    /// <param name="pCarrier">Kinematic state of the carrier.</param>
    /// <param name="pTarget">Kinematic state of the target.</param>
    /// <returns>The geometry.</returns>
    public static SensorGeometry ComputeGeometry(in Kinematic pCarrier, in Kinematic pTarget)
    {
        double lHorizontal = GeoMath.Distance(pCarrier.Position, pTarget.Position);
        double lVertical = pTarget.Position.Altitude - pCarrier.Position.Altitude;
        double lBearing = GeoMath.Bearing(pCarrier.Position, pTarget.Position);
        double lRelative = GeoMath.NormalizeHeading(lBearing - pCarrier.Orientation.Heading + 180) - 180;
        return new SensorGeometry(pCarrier, pTarget, Math.Sqrt((lHorizontal * lHorizontal) + (lVertical * lVertical)), lBearing, lRelative);
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

            Entity lCarrier = lSensor.Parent;
            List<Detection> lDetections = new List<Detection>();
            EquipmentData lData = lSensor.GetComponent<EquipmentData>();
            if (!lCarrier.IsNull && lCarrier.TryGetComponent(out Kinematic lCarrierKinematic) && lModel.IsActive(lData))
            {
                foreach (Entity lTarget in lTargets)
                {
                    if (lTarget == lCarrier)
                    {
                        continue;
                    }

                    SensorGeometry lGeometry = ComputeGeometry(lCarrierKinematic, lTarget.GetComponent<Kinematic>());
                    if (lModel.Detects(lData, lGeometry))
                    {
                        lDetections.Add(new Detection(lTarget, lGeometry.Range, lGeometry.Bearing, lGeometry.RelativeBearing));
                    }
                }
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
