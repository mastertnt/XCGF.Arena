using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Detection model of the equipments of type Radar, after the FighterRadar definition of XCGF.Data.
/// </summary>
/// <remarks>
/// The radar detects every target within MaxRange whose bearing, relative to the carrier heading, lies within half
/// of FieldOfView on either side. It detects nothing when its Mode is Off. A missing MaxRange or FieldOfView puts no
/// limit on range or angle.
/// </remarks>
public sealed class RadarModel : ISensorModel
{
    /// <summary>
    /// Equipment type this model handles.
    /// </summary>
    public const string SensorType = "Radar";

    /// <summary>
    /// Mode in which the radar neither emits nor detects.
    /// </summary>
    public const string OffMode = "Off";

    /// <inheritdoc/>
    public bool IsActive(EquipmentData pData)
    {
        return pData.Initialization.Get("Mode") is not EnumValue { Name: OffMode };
    }

    /// <inheritdoc/>
    public bool Detects(EquipmentData pData, in SensorGeometry pGeometry)
    {
        if (pData.Technological.GetNumber("MaxRange") is { } lMaxRange && pGeometry.Range > lMaxRange)
        {
            return false;
        }

        return pData.Technological.GetNumber("FieldOfView") is not { } lFieldOfView
            || Math.Abs(pGeometry.RelativeBearing) <= lFieldOfView / 2;
    }
}
