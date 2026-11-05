using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Detection model of the equipments of type Radar (docs/models/radar.md).
/// </summary>
/// <remarks>
/// The radar adds its Mode to the <see cref="SensorModel"/> behaviour: it neither emits nor detects when it is Off.
/// Inside its coverage it detects every target; the radar equation of the reference model, which needs the target
/// signature and a detection threshold, is left to a more detailed model registered in its place.
/// </remarks>
public sealed class RadarModel : SensorModel
{
    /// <summary>
    /// Equipment type this model handles.
    /// </summary>
    public new const string SensorType = "Radar";

    /// <summary>
    /// Mode in which the radar neither emits nor detects.
    /// </summary>
    public const string OffMode = "Off";

    /// <summary>
    /// Tells whether a radar emits, which makes it visible to the ESM of the other entities.
    /// </summary>
    /// <param name="pData">Data of the radar equipment.</param>
    /// <returns>True when the radar is operational and not Off.</returns>
    public static bool IsEmitting(EquipmentData pData)
    {
        return IsTrue(pData.Initialization.Get("Operational"), true)
            && pData.Initialization.Get("Mode") is not EnumValue { Name: OffMode };
    }

    /// <inheritdoc/>
    public override bool IsActive(EquipmentData pData) => IsEmitting(pData);
}
