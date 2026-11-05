using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;

namespace XCGF.Arena.Dis;

/// <summary>
/// Publishes an Entity State for every entity built from an entity archetype, at a fixed rate.
/// </summary>
/// <remarks>
/// The entity number is the Friflo entity id. The force and the marking come from the <see cref="Force"/> component
/// when the scenario sets one; otherwise the force is 0 (other) and the marking is the archetype name.
/// </remarks>
public sealed class DisSystem : QuerySystem<EntityIdentity, Kinematic>
{
    private const int MarkingLength = 11;

    private readonly IDisPublisher mPublisher;
    private TimeSpan? mLastPublished;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisSystem"/> class.
    /// </summary>
    /// <param name="pPublisher">Destination of the entity states.</param>
    /// <param name="pSite">DIS site number of the arena.</param>
    /// <param name="pApplication">DIS application number of the arena.</param>
    /// <param name="pInterval">Time between two publications of the same entity; zero publishes at every step.</param>
    public DisSystem(IDisPublisher pPublisher, ushort pSite = 1, ushort pApplication = 1, TimeSpan? pInterval = null)
    {
        ArgumentNullException.ThrowIfNull(pPublisher);
        mPublisher = pPublisher;
        Site = pSite;
        Application = pApplication;
        Interval = pInterval ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Gets the DIS site number of the arena.
    /// </summary>
    public ushort Site { get; }

    /// <summary>
    /// Gets the DIS application number of the arena.
    /// </summary>
    public ushort Application { get; }

    /// <summary>
    /// Gets the time between two publications of the same entity.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Builds the entity state of an entity.
    /// </summary>
    /// <param name="pEntity">The entity, with <see cref="EntityIdentity"/> and <see cref="Kinematic"/>.</param>
    /// <param name="pTimestamp">Simulation time of the state.</param>
    /// <returns>The entity state.</returns>
    public DisEntityState BuildState(Entity pEntity, TimeSpan pTimestamp)
    {
        EntityIdentity lIdentity = pEntity.GetComponent<EntityIdentity>();
        Kinematic lKinematic = pEntity.GetComponent<Kinematic>();
        pEntity.TryGetComponent(out Force lForce);
        string lMarking = lForce.Callsign ?? lIdentity.Archetype;
        if (lMarking.Length > MarkingLength)
        {
            lMarking = lMarking[..MarkingLength];
        }

        return new DisEntityState(
            new DisEntityId(Site, Application, checked((ushort)pEntity.Id)),
            (byte)lForce.Side,
            DisEntityType.Parse(lIdentity.DisType),
            DisConversion.ToGeocentric(lKinematic.Position),
            DisConversion.ToGeocentricVelocity(lKinematic),
            DisConversion.ToDisOrientation(lKinematic.Position, lKinematic.Orientation),
            lMarking,
            pTimestamp);
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        TimeSpan lNow = TimeSpan.FromSeconds(Tick.time);
        if (mLastPublished is { } lLast && lNow - lLast < Interval)
        {
            return;
        }

        mLastPublished = lNow;
        foreach (Entity lEntity in Query.Entities.ToArray())
        {
            mPublisher.Publish(BuildState(lEntity, lNow));
        }
    }
}
