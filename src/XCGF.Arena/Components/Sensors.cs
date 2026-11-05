using Friflo.Engine.ECS;

namespace XCGF.Arena.Components;

/// <summary>
/// Entity seen by a sensor at a step.
/// </summary>
/// <param name="Target">The entity seen.</param>
/// <param name="Range">Slant distance from the carrier to the target, in metres.</param>
/// <param name="Bearing">Azimuth from the carrier to the target, from true north, clockwise, in [0, 360) degrees.</param>
/// <param name="RelativeBearing">Bearing relative to the carrier heading, in [-180, 180) degrees, positive to the right.</param>
public readonly record struct Detection(Entity Target, double Range, double Bearing, double RelativeBearing);

/// <summary>
/// Detections of a sensor equipment at the last step it was evaluated.
/// </summary>
/// <param name="Items">The detections, empty when the sensor sees nothing or is off.</param>
/// <param name="Time">Simulation time of the last scan.</param>
public record struct SensorDetections(IReadOnlyList<Detection> Items, TimeSpan Time) : IComponent;
