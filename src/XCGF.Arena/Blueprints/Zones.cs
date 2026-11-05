using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;

namespace XCGF.Arena.Blueprints;

/// <summary>
/// Circular zone on the ground, carried by an entity of its own.
/// </summary>
/// <param name="Center">Centre of the zone. Its altitude is ignored.</param>
/// <param name="Radius">Radius, in metres.</param>
public record struct CircularZone(GeoPosition Center, double Radius) : IComponent
{
    /// <summary>
    /// Tells whether a position is inside the zone, whatever its altitude.
    /// </summary>
    /// <param name="pPosition">The position.</param>
    /// <returns>True when inside.</returns>
    public readonly bool Contains(GeoPosition pPosition) => GeoMath.Distance(Center, pPosition) <= Radius;
}

/// <summary>
/// Subscription of a blueprint to the entry of its entity into a zone, carried by an entity of its own. The
/// <see cref="Nodes.OnZoneEnter"/> event creates it; <see cref="ZoneDetectionSystem"/> reads it.
/// </summary>
/// <param name="Owner">Entity whose blueprint listens.</param>
/// <param name="Key">The event to fire.</param>
/// <param name="Zone">Entity carrying the <see cref="CircularZone"/>.</param>
/// <param name="Enabled">Whether the subscription is active.</param>
/// <param name="Inside">Whether the owner was inside at the previous step.</param>
public record struct ZoneSubscription(Entity Owner, EventKey Key, Entity Zone, bool Enabled, bool Inside) : IComponent;

/// <summary>
/// Fires the zone events: for each subscription, detects the owner going from outside to inside its zone.
/// </summary>
public sealed class ZoneDetectionSystem : QuerySystem<ZoneSubscription>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZoneDetectionSystem"/> class.
    /// </summary>
    /// <param name="pRuntime">The runtime to fire the events in.</param>
    public ZoneDetectionSystem(BlueprintRuntime pRuntime)
    {
        ArgumentNullException.ThrowIfNull(pRuntime);
        Runtime = pRuntime;
    }

    /// <summary>
    /// Gets the runtime the events are fired in.
    /// </summary>
    public BlueprintRuntime Runtime { get; }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        TimeSpan lNow = TimeSpan.FromSeconds(Tick.time);
        Query.ForEachEntity((ref ZoneSubscription pSubscription, Entity pEntity) =>
        {
            if (!pSubscription.Enabled
                || pSubscription.Owner.IsNull
                || pSubscription.Zone.IsNull
                || !pSubscription.Owner.TryGetComponent(out Kinematic lKinematic)
                || !pSubscription.Zone.TryGetComponent(out CircularZone lZone))
            {
                return;
            }

            bool lInside = lZone.Contains(lKinematic.Position);
            if (lInside && !pSubscription.Inside)
            {
                Runtime.Log($"#{pSubscription.Owner.Id} enters zone #{pSubscription.Zone.Id}");
                Runtime.Trigger(pSubscription.Owner, pSubscription.Key, new object?[] { pSubscription.Zone, lNow });
            }
            else if (!lInside && pSubscription.Inside)
            {
                Runtime.Log($"#{pSubscription.Owner.Id} leaves zone #{pSubscription.Zone.Id}");
            }

            // A value change, not a structural one: allowed while iterating.
            pSubscription.Inside = lInside;
        });
    }
}
