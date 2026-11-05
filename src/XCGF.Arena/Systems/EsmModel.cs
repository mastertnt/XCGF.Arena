using Friflo.Engine.ECS;
using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Detection model of the equipments of type ESM, such as a radar warning receiver (docs/models/esm.md).
/// </summary>
/// <remarks>
/// The ESM is passive: it is active while operational. Inside its coverage it detects a target that carries an
/// emitting radar (see <see cref="RadarModel.IsEmitting"/>) sharing at least one of its FrequencyBands; an ESM or a
/// radar without FrequencyBands hears every band. The propagation budget against Sensitivity is left to a more
/// detailed model.
/// </remarks>
public sealed class EsmModel : SensorModel
{
    /// <summary>
    /// Equipment type this model handles.
    /// </summary>
    public new const string SensorType = "ESM";

    /// <inheritdoc/>
    protected override bool Senses(EquipmentData pData, in SensorGeometry pGeometry)
    {
        IReadOnlySet<string> lBands = NamesOf(pData.Technological.Get("FrequencyBands"));
        foreach (Entity lEquipment in pGeometry.Target.GetEquipments())
        {
            if (lEquipment.GetComponent<Equipment>().Type != RadarModel.SensorType
                || !lEquipment.TryGetComponent(out EquipmentData lEmitter)
                || !RadarModel.IsEmitting(lEmitter))
            {
                continue;
            }

            IReadOnlySet<string> lEmitted = NamesOf(lEmitter.Technological.Get("FrequencyBands"));
            if (lBands.Count == 0 || lEmitted.Count == 0 || lBands.Overlaps(lEmitted))
            {
                return true;
            }
        }

        return false;
    }
}
