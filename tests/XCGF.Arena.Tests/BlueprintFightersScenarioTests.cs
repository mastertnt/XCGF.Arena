using Friflo.Engine.ECS;
using XCGF.Arena.Blueprints;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Scenarios;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Tests;

public class BlueprintFightersScenarioTests
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    private readonly StringWriter mLog = new StringWriter();
    private readonly ArenaSimulation mSimulation = new ArenaSimulation(TestLibrary.Example);
    private readonly BlueprintFightersScenario mScenario;

    public BlueprintFightersScenarioTests()
    {
        mScenario = new BlueprintFightersScenario(mLog);
        mScenario.Setup(mSimulation);
    }

    [Fact]
    public void Setup_SpawnsTwoFightersDrivenByBlueprintsAndTheEngagementZone()
    {
        Assert.Equal("BLUE1", mScenario.Blue.GetComponent<Force>().Callsign);
        Assert.Equal("RED1", mScenario.Red.GetComponent<Force>().Callsign);
        Assert.True(mScenario.Blue.HasComponent<BlueprintRef>());
        Assert.True(mScenario.Red.HasComponent<BlueprintRef>());
        Assert.Equal(25000, mScenario.EngagementZone.GetComponent<CircularZone>().Radius);
        Assert.Equal(new[] { "blue-intercept", "red-patrol" }, mSimulation.Blueprints.Graphs.Select(pGraph => pGraph.Name));
    }

    [Fact]
    public void Start_SilencesTheRadarsAndAssignsTheMovementTasks()
    {
        Run(TimeSpan.FromSeconds(3));

        Assert.Equal("Off", RadarMode(mScenario.Blue));
        Assert.Equal("Off", RadarMode(mScenario.Red));
        Assert.Equal("MoveTo", RunningTask(mScenario.Blue));
        Assert.Equal("FollowCourse", RunningTask(mScenario.Red));
        Assert.Equal(280, mScenario.Blue.GetComponent<Kinematic>().Speed);
    }

    [Fact]
    public void Blue_EntersTheZoneWithRadarOn_HoldsThenReturnsToBase()
    {
        RunUntil(() => mLog.ToString().Contains("BLUE1: in the engagement zone", StringComparison.Ordinal), TimeSpan.FromMinutes(10));
        Run(TimeSpan.FromSeconds(2));
        Assert.Equal("Search", RadarMode(mScenario.Blue));

        RunUntil(() => RunningTask(mScenario.Blue) == "HoldPosition", TimeSpan.FromMinutes(10));
        RunUntil(() => mLog.ToString().Contains("BLUE1: end of patrol", StringComparison.Ordinal), TimeSpan.FromSeconds(mScenario.CapDuration + 10));
        RunUntil(() => mLog.ToString().Contains("BLUE1: back at base", StringComparison.Ordinal), TimeSpan.FromMinutes(20));

        Assert.True(GeoMath.Distance(mScenario.Blue.GetComponent<Kinematic>().Position, mScenario.BlueStart) <= 1000);
        Assert.Equal(new[] { "MoveTo", "HoldPosition", "MoveTo" }, mScenario.Blue.GetComponent<AssignedTasks>().Items.Select(pTask => pTask.Definition.Name));
        Assert.All(mScenario.Blue.GetComponent<AssignedTasks>().Items, pTask => Assert.Equal("Success", pTask.Outcome));
    }

    [Fact]
    public void Red_PatrolsInALoopAndScansEachTimeItCrossesTheZone()
    {
        RunUntil(() => mLog.ToString().Contains("RED1: in the engagement zone", StringComparison.Ordinal), TimeSpan.FromMinutes(15));
        Run(TimeSpan.FromSeconds(2));
        Assert.Equal("Search", RadarMode(mScenario.Red));

        Run(TimeSpan.FromSeconds(mScenario.RedScanDuration + 2));
        Assert.Equal("Off", RadarMode(mScenario.Red));

        RunUntil(() => CountOf(mLog.ToString(), "RED1: in the engagement zone") >= 2, TimeSpan.FromMinutes(30));
        Assert.Equal("FollowCourse", RunningTask(mScenario.Red));
    }

    private static int CountOf(string pText, string pValue) => (pText.Length - pText.Replace(pValue, string.Empty, StringComparison.Ordinal).Length) / pValue.Length;

    private static string? RunningTask(Entity pFighter) =>
        pFighter.GetComponent<AssignedTasks>().Items.LastOrDefault(pTask => pTask.State == TaskState.Running)?.Definition.Name;

    private static string? RadarMode(Entity pFighter) =>
        (pFighter.FindEquipment("Radar").GetComponent<EquipmentData>().Initialization.Get("Mode") as EnumValue)?.Name;

    private void Run(TimeSpan pDuration)
    {
        TimeSpan lEnd = mSimulation.Elapsed + pDuration;
        while (mSimulation.Elapsed < lEnd)
        {
            Step();
        }
    }

    private void RunUntil(Func<bool> pCondition, TimeSpan pTimeout)
    {
        TimeSpan lEnd = mSimulation.Elapsed + pTimeout;
        while (!pCondition())
        {
            Assert.True(mSimulation.Elapsed < lEnd, "Timed out. Log:" + Environment.NewLine + mLog);
            Step();
        }
    }

    private void Step()
    {
        mSimulation.Step(sStep);
        mScenario.Update(mSimulation);
    }
}
