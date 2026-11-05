using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;

namespace XCGF.Arena.Systems;

/// <summary>
/// Moves the entities along their heading and pitch, at their speed, within their kinematic limits.
/// </summary>
/// <remarks>
/// The speed is kept between the stall speed and the maximum speed, and the altitude never exceeds the ceiling.
/// </remarks>
public sealed class KinematicSystem : QuerySystem<Kinematic>
{
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

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        double lSeconds = Tick.deltaTime;
        Query.ForEachEntity((ref Kinematic pKinematic, Entity pEntity) =>
        {
            pEntity.TryGetComponent(out KinematicLimits lLimits);
            pKinematic.Speed = ClampSpeed(pKinematic.Speed, lLimits);
            if (pKinematic.Speed <= 0)
            {
                return;
            }

            double lPitch = pKinematic.Orientation.Pitch * Math.PI / 180;
            double lDistance = pKinematic.Speed * lSeconds;
            GeoPosition lPosition = GeoMath.Offset(pKinematic.Position, pKinematic.Orientation.Heading, lDistance * Math.Cos(lPitch));
            double lAltitude = pKinematic.Position.Altitude + (lDistance * Math.Sin(lPitch));
            if (lLimits.Ceiling is { } lCeiling && lAltitude > lCeiling)
            {
                lAltitude = lCeiling;
            }

            pKinematic.Position = lPosition with { Altitude = lAltitude };
        });
    }
}
