using Friflo.Engine.ECS;
using XCGF.Arena.Blueprints;
using XCGF.Arena.Blueprints.Nodes;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Scenarios;

/// <summary>
/// Two Fighters of opposing sides, 150 km apart at 8000 m, entirely driven by blueprint graphs around an engagement zone.
/// </summary>
/// <remarks>
/// <para>
/// BLUE1 (interception): radar off, it flies to a combat air patrol point inside the engagement zone, holds there for
/// <see cref="CapDuration"/>, then flies back to its starting point. It switches its radar to Search when it enters the
/// zone.
/// </para>
/// <para>
/// RED1 (patrol): radar off, it flies a looping racetrack east of the zone, whose western leg crosses the zone. Each time
/// it enters the zone, it switches its radar to Search for <see cref="RedScanDuration"/>, then off again.
/// </para>
/// The scenario script does nothing at each step: the graphs, run by <see cref="BlueprintSystem"/>, drive the fighters.
/// </remarks>
public sealed class BlueprintFightersScenario : IScenario
{
    private readonly TextWriter mLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintFightersScenario"/> class.
    /// </summary>
    /// <param name="pLog">The writer that receives the log of the blueprints, or null for none.</param>
    public BlueprintFightersScenario(TextWriter? pLog = null)
    {
        mLog = pLog ?? TextWriter.Null;
    }

    /// <summary>
    /// Gets the starting position of the blue fighter.
    /// </summary>
    public GeoPosition BlueStart { get; init; } = new GeoPosition(48.0, 1.0, 8000);

    /// <summary>
    /// Gets the starting position of the red fighter.
    /// </summary>
    public GeoPosition RedStart { get; init; } = new GeoPosition(48.0, 3.0, 8000);

    /// <summary>
    /// Gets the centre of the engagement zone.
    /// </summary>
    public GeoPosition ZoneCenter { get; init; } = new GeoPosition(48.0, 2.0, 0);

    /// <summary>
    /// Gets the radius of the engagement zone, in metres.
    /// </summary>
    public double ZoneRadius { get; init; } = 25000;

    /// <summary>
    /// Gets the combat air patrol point of the blue fighter.
    /// </summary>
    public GeoPosition CapPoint { get; init; } = new GeoPosition(48.0, 1.75, 8000);

    /// <summary>
    /// Gets the speed of the blue fighter towards its patrol point, in metres per second.
    /// </summary>
    public double InterceptSpeed { get; init; } = 280;

    /// <summary>
    /// Gets how long the blue fighter holds at its patrol point, in seconds.
    /// </summary>
    public double CapDuration { get; init; } = 120;

    /// <summary>
    /// Gets the waypoints of the racetrack of the red fighter, flown in a loop.
    /// </summary>
    public IReadOnlyList<CourseWaypoint> RedRacetrack { get; init; } = new[]
    {
        new CourseWaypoint(new GeoPosition(48.2, 2.5, 8000)),
        new CourseWaypoint(new GeoPosition(48.0, 2.25, 8000)),
        new CourseWaypoint(new GeoPosition(47.8, 2.5, 8000)),
        new CourseWaypoint(new GeoPosition(48.0, 2.75, 8000)),
    };

    /// <summary>
    /// Gets how long the red fighter keeps its radar on after entering the zone, in seconds.
    /// </summary>
    public double RedScanDuration { get; init; } = 60;

    /// <inheritdoc/>
    public string Name => "Two fighters (blueprints)";

    /// <summary>
    /// Gets the blue fighter, once set up.
    /// </summary>
    public Entity Blue { get; private set; }

    /// <summary>
    /// Gets the red fighter, once set up.
    /// </summary>
    public Entity Red { get; private set; }

    /// <summary>
    /// Gets the entity carrying the <see cref="CircularZone"/> of the engagement zone, once set up.
    /// </summary>
    public Entity EngagementZone { get; private set; }

    /// <inheritdoc/>
    public void Setup(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
        pArena.Blueprints.Logged += mLog.WriteLine;
        EngagementZone = pArena.Store.CreateEntity(new CircularZone(ZoneCenter, ZoneRadius));
        Blue = Spawn(pArena, "BLUE1", ForceSide.Friendly, BlueStart, CapPoint);
        Red = Spawn(pArena, "RED1", ForceSide.Opposing, RedStart, RedRacetrack[0].Position);
        pArena.Blueprints.Attach(Blue, pArena.Blueprints.RegisterGraph(CreateInterceptGraph(pArena)));
        pArena.Blueprints.Attach(Red, pArena.Blueprints.RegisterGraph(CreatePatrolGraph(pArena)));
    }

    /// <inheritdoc/>
    public void Update(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
    }

    /// <summary>
    /// Builds the graph of the blue fighter.
    /// </summary>
    /// <param name="pArena">The simulation.</param>
    /// <returns>The graph.</returns>
    /// <remarks>
    /// [OnStart] -> [Radar.Mode = Off] -> [MoveTo CapPoint at InterceptSpeed] -> [HoldPosition CapDuration]
    ///    -> [Log] -> [MoveTo BlueStart at Cruise] -> [Log]; a failed task logs its outcome.
    /// [OnZoneEnter zone, once] -> [Log] -> [Radar.Mode = Search].
    /// </remarks>
    public BlueprintGraph CreateInterceptGraph(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
        SetEquipmentParameter lSetParameter = new SetEquipmentParameter(pArena.Library);
        BlueprintGraph lGraph = new BlueprintGraph("blue-intercept");

        int lStart = lGraph.Add(new OnStart());
        int lRadarOff = lGraph.Add(lSetParameter, "Radar", "Mode", "Off");
        int lToCap = lGraph.Add(new MoveTo(pArena.Tasks, pArena.Library), CapPoint, InterceptSpeed);
        int lHold = lGraph.Add(new HoldPosition(pArena.Tasks, pArena.Library), CapDuration);
        int lCapDone = lGraph.Add(new LogMessage(), "BLUE1: end of patrol, returning to base");
        int lToBase = lGraph.Add(new MoveTo(pArena.Tasks, pArena.Library), BlueStart, null, "Cruise");
        int lHome = lGraph.Add(new LogMessage(), "BLUE1: back at base");
        int lFailed = lGraph.Add(new LogMessage());
        lGraph.Exec(lStart, lRadarOff);
        lGraph.Exec(lRadarOff, lToCap);
        lGraph.Exec(lToCap, lHold, TaskNode.SuccessPin);
        lGraph.Exec(lHold, lCapDone, TaskNode.SuccessPin);
        lGraph.Exec(lCapDone, lToBase);
        lGraph.Exec(lToBase, lHome, TaskNode.SuccessPin);
        foreach (int lTask in new[] { lToCap, lHold, lToBase })
        {
            lGraph.Exec(lTask, lFailed, TaskNode.FailurePin);
            lGraph.Data(lTask, 0, lFailed, 0);
        }

        int lEnter = lGraph.Add(new OnZoneEnter(RetriggerPolicy.Once), EngagementZone);
        int lEntered = lGraph.Add(new LogMessage(), "BLUE1: in the engagement zone, radar on");
        int lRadarOn = lGraph.Add(lSetParameter, "Radar", "Mode", "Search");
        lGraph.Exec(lEnter, lEntered);
        lGraph.Exec(lEntered, lRadarOn);
        return lGraph;
    }

    /// <summary>
    /// Builds the graph of the red fighter.
    /// </summary>
    /// <param name="pArena">The simulation.</param>
    /// <returns>The graph.</returns>
    /// <remarks>
    /// [OnStart] -> [Radar.Mode = Off] -> [FollowCourse RedRacetrack at Cruise, looped].
    /// [OnZoneEnter zone, ignored while running] -> [Log] -> [Radar.Mode = Search] -> [Delay RedScanDuration]
    ///    -> [Radar.Mode = Off].
    /// </remarks>
    public BlueprintGraph CreatePatrolGraph(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
        SetEquipmentParameter lSetParameter = new SetEquipmentParameter(pArena.Library);
        BlueprintGraph lGraph = new BlueprintGraph("red-patrol");

        int lStart = lGraph.Add(new OnStart());
        int lRadarOff = lGraph.Add(lSetParameter, "Radar", "Mode", "Off");
        int lPatrol = lGraph.Add(new FollowCourse(pArena.Tasks, pArena.Library), RedRacetrack, "Cruise", true);
        lGraph.Exec(lStart, lRadarOff);
        lGraph.Exec(lRadarOff, lPatrol);

        int lEnter = lGraph.Add(new OnZoneEnter(RetriggerPolicy.Ignore), EngagementZone);
        int lEntered = lGraph.Add(new LogMessage(), "RED1: in the engagement zone, radar on");
        int lScan = lGraph.Add(lSetParameter, "Radar", "Mode", "Search");
        int lWait = lGraph.Add(new Delay(), RedScanDuration);
        int lSilent = lGraph.Add(lSetParameter, "Radar", "Mode", "Off");
        lGraph.Exec(lEnter, lEntered);
        lGraph.Exec(lEntered, lScan);
        lGraph.Exec(lScan, lWait);
        lGraph.Exec(lWait, lSilent);
        return lGraph;
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
}
