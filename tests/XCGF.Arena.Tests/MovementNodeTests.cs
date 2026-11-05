using Friflo.Engine.ECS;
using XCGF.Arena.Blueprints;
using XCGF.Arena.Blueprints.Nodes;
using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Tests;

public class MovementNodeTests
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    private readonly ArenaSimulation mSimulation = new ArenaSimulation(TestLibrary.Example);
    private readonly List<string> mLog = new List<string>();

    public MovementNodeTests()
    {
        mSimulation.Blueprints.Logged += mLog.Add;
    }

    [Fact]
    public void MoveTo_ThenHoldPosition_RunInSequenceAndGiveTheirOutcome()
    {
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter");
        GeoPosition lDestination = new GeoPosition(0, 0.05, 0);
        BlueprintGraph lGraph = new BlueprintGraph("move-then-hold");
        int lStart = lGraph.Add(new OnStart());
        int lMove = lGraph.Add(new MoveTo(mSimulation.Tasks, mSimulation.Library), lDestination, 300.0);
        int lHold = lGraph.Add(new HoldPosition(mSimulation.Tasks, mSimulation.Library), 60.0);
        int lOutcome = lGraph.Add(new LogMessage());
        int lFailed = lGraph.Add(new LogMessage(), "failed");
        lGraph.Exec(lStart, lMove);
        lGraph.Exec(lMove, lHold, TaskNode.SuccessPin);
        lGraph.Exec(lMove, lFailed, TaskNode.FailurePin);
        lGraph.Exec(lHold, lOutcome, TaskNode.SuccessPin);
        lGraph.Data(lHold, 0, lOutcome, 0);
        mSimulation.Blueprints.Attach(lFighter, mSimulation.Blueprints.RegisterGraph(lGraph));

        mSimulation.Run(TimeSpan.FromSeconds(3), sStep);
        TaskInstance lMoveTask = lFighter.GetComponent<AssignedTasks>().Items.Single();
        Assert.Equal("MoveTo", lMoveTask.Definition.Name);
        Assert.Equal(TaskState.Running, lMoveTask.State);
        Assert.Equal(300, lFighter.GetComponent<Kinematic>().Speed);

        RunUntil(() => mLog.Any(pLine => pLine.EndsWith(" Success", StringComparison.Ordinal) && !pLine.Contains("task", StringComparison.Ordinal)), TimeSpan.FromMinutes(10));
        Assert.Equal(new[] { "MoveTo", "HoldPosition" }, lFighter.GetComponent<AssignedTasks>().Items.Select(pTask => pTask.Definition.Name));
        Assert.All(lFighter.GetComponent<AssignedTasks>().Items, pTask => Assert.Equal("Success", pTask.Outcome));
        Assert.DoesNotContain(mLog, pLine => pLine.EndsWith("failed", StringComparison.Ordinal));
        Assert.Equal(0, mSimulation.Store.Query<FiberState>().Count);
    }

    [Fact]
    public void MoveTo_Cancelled_FollowsTheFailureOutput()
    {
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter");
        BlueprintGraph lGraph = new BlueprintGraph("cancelled");
        int lStart = lGraph.Add(new OnStart());
        int lMove = lGraph.Add(new MoveTo(mSimulation.Tasks, mSimulation.Library), new GeoPosition(0, 179, 0), null, "Cruise");
        int lOutcome = lGraph.Add(new LogMessage());
        lGraph.Exec(lStart, lMove);
        lGraph.Exec(lMove, lOutcome, TaskNode.FailurePin);
        lGraph.Data(lMove, 0, lOutcome, 0);
        mSimulation.Blueprints.Attach(lFighter, mSimulation.Blueprints.RegisterGraph(lGraph));

        mSimulation.Run(TimeSpan.FromSeconds(5), sStep);
        Assert.Equal(230, lFighter.GetComponent<Kinematic>().Speed);
        mSimulation.Tasks.RaiseEvent(lFighter, "OrderCancelled");
        mSimulation.Run(TimeSpan.FromSeconds(3), sStep);

        Assert.Contains(mLog, pLine => pLine.EndsWith(" Aborted", StringComparison.Ordinal) && !pLine.Contains("task", StringComparison.Ordinal));
    }

    [Fact]
    public void FollowCourse_GoesThroughTheWaypointsOfItsCourse()
    {
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter");
        IReadOnlyList<CourseWaypoint> lCourse = new[]
        {
            new CourseWaypoint(new GeoPosition(0, 0.05, 0), 300),
            new CourseWaypoint(new GeoPosition(0.05, 0.05, 0)),
        };
        BlueprintGraph lGraph = new BlueprintGraph("course");
        int lStart = lGraph.Add(new OnStart());
        int lFollow = lGraph.Add(new FollowCourse(mSimulation.Tasks, mSimulation.Library), lCourse, null, false);
        lGraph.Exec(lStart, lFollow);
        lGraph.Exec(lFollow, lGraph.Add(new LogMessage(), "course done"), TaskNode.SuccessPin);
        mSimulation.Blueprints.Attach(lFighter, mSimulation.Blueprints.RegisterGraph(lGraph));

        mSimulation.Run(TimeSpan.FromSeconds(3), sStep);
        Assert.Equal(300, lFighter.GetComponent<Kinematic>().Speed);

        RunUntil(() => mLog.Any(pLine => pLine.EndsWith("course done", StringComparison.Ordinal)), TimeSpan.FromMinutes(20));
        Assert.True(GeoMath.Distance(lFighter.GetComponent<Kinematic>().Position, new GeoPosition(0.05, 0.05, 0)) <= 500);
        Assert.Equal(new EnumValue("HLAfalse", 0), lFighter.GetComponent<AssignedTasks>().Items.Single().Parameters["Loop"]);
    }

    [Fact]
    public void MoveTo_TakesItsDestinationFromPureNodes()
    {
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter");

        // Hold still during the first step, which moves the fighter before the blueprint runs.
        lFighter.AddComponent(new KinematicLimits(null, null, null));
        lFighter.GetComponent<Kinematic>().Speed = 0;
        BlueprintGraph lGraph = new BlueprintGraph("offset");
        int lStart = lGraph.Add(new OnStart());
        int lMove = lGraph.Add(new MoveTo(mSimulation.Tasks, mSimulation.Library));
        int lSelf = lGraph.Add(new Self());
        int lHere = lGraph.Add(new GetPosition());
        int lEast = lGraph.Add(new OffsetPosition(), null, 90.0, 5000.0);
        lGraph.Exec(lStart, lMove);
        lGraph.Data(lSelf, 0, lHere, 0);
        lGraph.Data(lHere, 0, lEast, 0);
        lGraph.Data(lEast, 0, lMove, 0);
        mSimulation.Blueprints.Attach(lFighter, mSimulation.Blueprints.RegisterGraph(lGraph));
        GeoPosition lExpected = GeoMath.Offset(lFighter.GetComponent<Kinematic>().Position, 90, 5000);

        mSimulation.Step(sStep);

        TaskInstance lTask = lFighter.GetComponent<AssignedTasks>().Items.Single();
        GeoPosition lDestination = GeoPosition.FromValue(lTask.Parameters["Destination"]!);
        Assert.Equal(lExpected.Longitude, lDestination.Longitude, 9);
        Assert.Equal(lExpected.Latitude, lDestination.Latitude, 9);
    }

    [Fact]
    public void OnTaskEnded_FiresOncePerEndedTaskItWatches()
    {
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter");
        BlueprintGraph lGraph = new BlueprintGraph("task-ended");
        int lMoveEnded = lGraph.Add(new OnTaskEnded(), "MoveTo");
        int lAnyEnded = lGraph.Add(new OnTaskEnded());
        int lMoveLog = lGraph.Add(new LogMessage());
        int lAnyLog = lGraph.Add(new LogMessage());
        lGraph.Exec(lMoveEnded, lMoveLog);
        lGraph.Exec(lAnyEnded, lAnyLog);
        lGraph.Data(lMoveEnded, 1, lMoveLog, 0);
        lGraph.Data(lAnyEnded, 0, lAnyLog, 0);
        mSimulation.Blueprints.Attach(lFighter, mSimulation.Blueprints.RegisterGraph(lGraph));

        mSimulation.Tasks.Assign(lFighter, "HoldPosition", new Dictionary<string, DataValue> { ["Duration"] = new NumberValue(10) });
        mSimulation.Tasks.Assign(lFighter, "MoveTo", new Dictionary<string, DataValue> { ["Destination"] = new GeoPosition(0, 179, 0).ToValue() });
        mSimulation.Run(TimeSpan.FromSeconds(5), sStep);
        mSimulation.Tasks.RaiseEvent(lFighter, "OrderCancelled");
        mSimulation.Run(TimeSpan.FromSeconds(20), sStep);

        Assert.Single(mLog, pLine => pLine.EndsWith(" Aborted", StringComparison.Ordinal));
        Assert.Single(mLog, pLine => pLine.EndsWith(" MoveTo", StringComparison.Ordinal));
        Assert.Single(mLog, pLine => pLine.EndsWith(" HoldPosition", StringComparison.Ordinal));
    }

    private void RunUntil(Func<bool> pCondition, TimeSpan pTimeout)
    {
        TimeSpan lEnd = mSimulation.Elapsed + pTimeout;
        while (!pCondition())
        {
            Assert.True(mSimulation.Elapsed < lEnd, "Timed out.");
            mSimulation.Step(sStep);
        }
    }
}
