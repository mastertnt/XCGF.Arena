using XCGF.Arena.Components;
using XCGF.Arena.Ecs;

namespace XCGF.Arena.Systems;

/// <summary>
/// Moves the entities along their heading and pitch, at their speed, within their kinematic limits.
/// </summary>
/// <remarks>
/// The speed is kept between the stall speed and the maximum speed, and the altitude never exceeds the ceiling.
/// </remarks>
public sealed class KinematicSystem : ISystem
{
    /// <inheritdoc/>
    public void Update(World pWorld, SimulationTime pTime)
    {
        ArgumentNullException.ThrowIfNull(pWorld);
        double lSeconds = pTime.Delta.TotalSeconds;
        foreach (Entity lEntity in pWorld.Query<Kinematic>())
        {
            ref Kinematic lKinematic = ref pWorld.Get<Kinematic>(lEntity);
            pWorld.TryGet(lEntity, out KinematicLimits lLimits);
            lKinematic.Speed = ClampSpeed(lKinematic.Speed, lLimits);
            if (lKinematic.Speed <= 0)
            {
                continue;
            }

            double lPitch = lKinematic.Orientation.Pitch * Math.PI / 180;
            double lDistance = lKinematic.Speed * lSeconds;
            GeoPosition lPosition = GeoMath.Offset(lKinematic.Position, lKinematic.Orientation.Heading, lDistance * Math.Cos(lPitch));
            double lAltitude = lKinematic.Position.Altitude + (lDistance * Math.Sin(lPitch));
            if (lLimits.Ceiling is { } lCeiling && lAltitude > lCeiling)
            {
                lAltitude = lCeiling;
            }

            lKinematic.Position = lPosition with { Altitude = lAltitude };
        }
    }

    /// <summary>
    /// Brings a speed within the limits.
    /// </summary>
    /// <param name="pSpeed">The speed, in metres per second.</param>
    /// <param name="pLimits">The limits.</param>
    /// <returns>The bounded speed.</returns>
    public static double ClampSpeed(double pSpeed, KinematicLimits pLimits)
    {
        double lSpeed = pSpeed;
        if (pLimits.MaxSpeed is { } lMax && lSpeed > lMax)
        {
            lSpeed = lMax;
        }

        if (pLimits.StallSpeed is { } lStall && lSpeed < lStall)
        {
            lSpeed = lStall;
        }

        return lSpeed;
    }
}
