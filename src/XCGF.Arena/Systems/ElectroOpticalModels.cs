using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Detection model of the equipments of type Infrared, such as an IRST (docs/models/infrared-sensor.md).
/// </summary>
/// <remarks>
/// Passive: active while operational, it detects every target inside its coverage. The reference range derived from
/// the infrared signature of the target and the weather is left to a more detailed model.
/// </remarks>
public sealed class InfraredModel : SensorModel
{
    /// <summary>
    /// Equipment type this model handles.
    /// </summary>
    public new const string SensorType = "Infrared";
}

/// <summary>
/// Detection model of the equipments of type Visual (docs/models/visual-sensor.md).
/// </summary>
/// <remarks>
/// Passive: active while operational, it detects every target inside its coverage. Daylight, night capability and
/// visibility are left to a more detailed model.
/// </remarks>
public sealed class VisualModel : SensorModel
{
    /// <summary>
    /// Equipment type this model handles.
    /// </summary>
    public new const string SensorType = "Visual";
}
