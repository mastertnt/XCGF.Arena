using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Geometry of a candidate target as seen from the carrier of a sensor.
/// </summary>
/// <param name="Carrier">Kinematic state of the carrier.</param>
/// <param name="Target">Kinematic state of the candidate target.</param>
/// <param name="Range">Slant distance, in metres.</param>
/// <param name="Bearing">Azimuth from the carrier, in [0, 360) degrees.</param>
/// <param name="RelativeBearing">Bearing relative to the carrier heading, in [-180, 180) degrees.</param>
public readonly record struct SensorGeometry(Kinematic Carrier, Kinematic Target, double Range, double Bearing, double RelativeBearing);

/// <summary>
/// Detection model of one type of sensor, selected by the <c>type</c> of its equipment definition (e.g. Radar).
/// </summary>
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
