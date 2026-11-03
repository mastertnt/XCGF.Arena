using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Scenarios;

/// <summary>
/// Two Fighters of opposing sides, 150 km apart at 8000 m, fly towards each other's starting point.
/// </summary>
/// <remarks>
/// Each fighter is assigned MoveTo towards the other's starting point and FireOnTarget on the other. The script
/// makes the "InRange(Weapon, Target)" condition hold when the target is between the MinRange and the MaxRange of the
/// shooter's first missile, which starts FireOnTarget, and it ceases fire 30 seconds later.
/// </remarks>
public sealed class TwoFightersScenario : IScenario
{
    /// <summary>
    /// Expression of the activation condition of FireOnTarget in the XCGF.Data library.
    /// </summary>
    public const string InRangeCondition = "InRange(Weapon, Target)";

    private static readonly TimeSpan sEngagementDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the starting position of the blue fighter.
    /// </summary>
    public GeoPosition BlueStart { get; init; } = new GeoPosition(48.0, 1.0, 8000);

    /// <summary>
    /// Gets the starting position of the red fighter.
    /// </summary>
    public GeoPosition RedStart { get; init; } = new GeoPosition(48.0, 3.0, 8000);

    /// <inheritdoc/>
    public string Name => "Two fighters";

    /// <summary>
    /// Gets the blue fighter, once set up.
    /// </summary>
    public Entity Blue { get; private set; }

    /// <summary>
    /// Gets the red fighter, once set up.
    /// </summary>
    public Entity Red { get; private set; }

    /// <inheritdoc/>
    public void Setup(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
        Blue = Spawn(pArena, "BLUE1", ForceSide.Friendly, BlueStart, RedStart);
        Red = Spawn(pArena, "RED1", ForceSide.Opposing, RedStart, BlueStart);
        pArena.Tasks.RegisterCondition(InRangeCondition, IsTargetInRange);
        Engage(pArena, Blue, Red, RedStart);
        Engage(pArena, Red, Blue, BlueStart);
    }

    /// <inheritdoc/>
    public void Update(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
        foreach (Entity lFighter in new[] { Blue, Red })
        {
            foreach (TaskInstance lTask in lFighter.GetComponent<AssignedTasks>().Items)
            {
                if (lTask.Definition.Name == "FireOnTarget" && lTask.State == TaskState.Running
                    && pArena.Elapsed - lTask.StartedAt >= sEngagementDuration)
                {
                    pArena.Tasks.RaiseEvent(lFighter, "CeaseFire");
                }
            }
        }
    }

    private static Entity Spawn(ArenaSimulation pArena, string pCallsign, ForceSide pSide, GeoPosition pStart, GeoPosition pHeadingTo)
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue>
        {
            ["Kinematic.Position"] = pStart.ToValue(),
            ["Kinematic.Orientation"] = new Orientation(GeoMath.Bearing(pStart, pHeadingTo), 0, 0).ToValue(),
        };
        Entity lFighter = pArena.Factory.SpawnEntity("Fighter", lOverrides);
        lFighter.AddComponent(new Force(pSide, pCallsign));
        return lFighter;
    }

    private static void Engage(ArenaSimulation pArena, Entity pShooter, Entity pTarget, GeoPosition pDestination)
    {
        pArena.Tasks.Assign(pShooter, "MoveTo", new Dictionary<string, DataValue>
        {
            ["Destination"] = RecordValue.Of(("Latitude", new NumberValue(pDestination.Latitude)), ("Longitude", new NumberValue(pDestination.Longitude))),
        });
        pArena.Tasks.Assign(pShooter, "FireOnTarget", new Dictionary<string, DataValue>
        {
            ["Target"] = new ReferenceValue(pTarget),
            ["Weapon"] = new ReferenceValue(pShooter.FindEquipment("Missile_1")),
        });
    }

    private static bool IsTargetInRange(TaskContext pContext)
    {
        if (pContext.Task.Parameters.GetValueOrDefault("Target") is not ReferenceValue { Target: { IsNull: false } lTarget }
            || pContext.Task.Parameters.GetValueOrDefault("Weapon") is not ReferenceValue { Target: { IsNull: false } lWeapon })
        {
            return false;
        }

        ParameterValues lWeaponData = lWeapon.GetComponent<EquipmentData>().Technological;
        double lRange = SensorSystem.ComputeGeometry(pContext.Carrier.GetComponent<Kinematic>(), lTarget.GetComponent<Kinematic>()).Range;
        return lRange >= (lWeaponData.GetNumber("MinRange") ?? 0) && lRange <= (lWeaponData.GetNumber("MaxRange") ?? double.MaxValue);
    }
}
