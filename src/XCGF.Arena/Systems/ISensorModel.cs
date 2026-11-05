using Friflo.Engine.ECS;
using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Geometry of a candidate target as seen from the carrier of a sensor.
/// </summary>
/// <param name="Target">The candidate target entity.</param>
/// <param name="Carrier">Kinematic state of the carrier.</param>
/// <param name="TargetKinematic">Kinematic state of the candidate target.</param>
/// <param name="Range">Slant distance, in metres.</param>
/// <param name="Bearing">Azimuth from the carrier, in [0, 360) degrees.</param>
/// <param name="RelativeBearing">Bearing relative to the carrier heading, in [-180, 180) degrees.</param>
/// <param name="Elevation">Elevation of the target above the horizontal plane of the carrier, in [-90, 90] degrees.</param>
public readonly record struct SensorGeometry(
    Entity Target,
    Kinematic Carrier,
    Kinematic TargetKinematic,
    double Range,
    double Bearing,
    double RelativeBearing,
    double Elevation);

/// <summary>
/// Detection model of one type of sensor, selected by the <c>type</c> of its equipment definition (e.g. Radar).
/// </summary>
/// <remarks>
/// <see cref="SensorModel"/> implements the behaviour shared by every sensor of XCGF.Data; derive from it rather than
/// implementing this interface directly, unless the model ignores the Sensor coverage.
/// </remarks>
public interface ISensorModel
{
    /// <summary>
    /// Tells whether the sensor is emitting or listening at all, before any target is considered.
    /// </summary>
    /// <param name="pData">Data of the sensor equipment.</param>
    /// <returns>True when the sensor can detect.</returns>
    bool IsActive(EquipmentData pData);

    /// <summary>
    /// Tells whether the sensor detects a candidate target.
    /// </summary>
    /// <param name="pData">Data of the sensor equipment.</param>
    /// <param name="pGeometry">Geometry of the target as seen from the carrier.</param>
    /// <returns>True when the target is detected.</returns>
    bool Detects(EquipmentData pData, in SensorGeometry pGeometry);
}
