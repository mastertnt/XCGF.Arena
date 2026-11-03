using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Ecs;
using XCGF.Arena.Spawning;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Tests;

public class TaskSystemTests
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    private readonly World mWorld = new World();
    private readonly ArchetypeFactory mFactory;
    private readonly TaskSystem mTasks;
    private readonly Simulation mSimulation;

    public TaskSystemTests()
    {
        mFactory = new ArchetypeFactory(mWorld, TestLibrary.Example);
        mTasks = new TaskSystem(TestLibrary.Example);
        mSimulation = new Simulation(mWorld).AddSystem(mTasks).AddSystem(new KinematicSystem());
    }

    [Fact]
    public void Assign_FillsDefaultsAndDefaultsTheTaskeeToTheCarrier()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        TaskInstance lTask = mTasks.Assign(mWorld, lFighter, "MoveTo", Destination(0, 0.1));

        Assert.Equal(TaskState.Pending, lTask.State);
        Assert.Equal(new NumberValue(250), lTask.Parameters["Speed"]);
        Assert.Equal(new ReferenceValue(lFighter), lTask.Parameters["Executor"]);
        Assert.Null(lTask.Parameters["TravelTime"]);
        Assert.Same(lTask, mWorld.Get<AssignedTasks>(lFighter).Items.Single());
    }

    [Fact]
    public void Assign_WithoutRequiredParameter_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mTasks.Assign(mWorld, lFighter, "MoveTo"));

        Assert.Contains("Required parameter 'Destination' is missing", lError.Message);
    }

    [Fact]
    public void Assign_TaskTheCarrierCannotPerform_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        Assert.Throws<XcgfDataException>(() => mTasks.Assign(mWorld, lFighter, "ChangeFormation"));
    }

    [Fact]
    public void Assign_ValueBreakingConstraint_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Entity lTarget = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lValues = new Dictionary<string, DataValue>
        {
            ["Target"] = new ReferenceValue(lTarget),
            ["Salvo"] = new IntegerValue(3),
        };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mTasks.Assign(mWorld, lFighter, "FireOnTarget", lValues));

        Assert.Contains("'Salvo' = 3 must be <= 2", lError.Message);
    }

    [Fact]
    public void Assign_OutParameterOrUnknownParameter_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lOut = new Dictionary<string, DataValue>(Destination(0, 1)) { ["TravelTime"] = new NumberValue(1) };
        Dictionary<string, DataValue> lUnknown = new Dictionary<string, DataValue>(Destination(0, 1)) { ["Altitude"] = new NumberValue(1) };

        Assert.Throws<XcgfDataException>(() => mTasks.Assign(mWorld, lFighter, "MoveTo", lOut));
        Assert.Throws<XcgfDataException>(() => mTasks.Assign(mWorld, lFighter, "MoveTo", lUnknown));
    }

    [Fact]
    public void MoveTo_FliesToDestinationAndSucceeds()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        TaskInstance lTask = mTasks.Assign(mWorld, lFighter, "MoveTo", Destination(0.1, 0.1));
        double lDistance = GeoMath.Distance(default, new GeoPosition(0.1, 0.1, 0));

        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Running, lTask.State);
        Assert.Equal(45, mWorld.Get<Kinematic>(lFighter).Orientation.Heading, 0);

        RunUntil(() => lTask.State == TaskState.Ended, TimeSpan.FromHours(1));

        Assert.Equal("Success", lTask.Outcome);
        Assert.True(GeoMath.Distance(mWorld.Get<Kinematic>(lFighter).Position, new GeoPosition(0.1, 0.1, 0)) <= 500);
        double lTravelTime = lTask.Parameters["TravelTime"]!.AsNumber()!.Value;
        Assert.InRange(lTravelTime, (lDistance - 500) / 250 - 2, (lDistance / 250) + 2);
    }

    [Fact]
    public void MoveTo_UnreachedAfterOneHour_Fails()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        TaskInstance lTask = mTasks.Assign(mWorld, lFighter, "MoveTo", Destination(0, 179));

        mSimulation.Run(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        Assert.Equal(TaskState.Ended, lTask.State);
        Assert.Equal("Failure", lTask.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(61), lTask.EndedAt);
    }

    [Fact]
    public void FollowFlightPlan_StartsWhenMoveToSucceeds()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        TaskInstance lMove = mTasks.Assign(mWorld, lFighter, "MoveTo", Destination(0, 0.02));
        Dictionary<string, DataValue> lPlan = new Dictionary<string, DataValue>
        {
            ["Plan"] = new ArrayValue(new DataValue[] { RecordValue.Of(("Position", new GeoPosition(0, 1, 0).ToValue()), ("Speed", new NumberValue(250))) }),
        };
        TaskInstance lFollow = mTasks.Assign(mWorld, lFighter, "FollowFlightPlan", lPlan);
        bool lCompleted = false;
        mTasks.RegisterCustomTrigger("FlightPlanCompleted", (pContext, pTrigger) => lCompleted);

        RunUntil(() => lMove.State == TaskState.Ended, TimeSpan.FromMinutes(10));

        Assert.Equal("Success", lMove.Outcome);
        Assert.Equal(TaskState.Running, lFollow.State);
        Assert.Equal(lMove.EndedAt, lFollow.StartedAt);
        Assert.Equal(new IntegerValue(0), lFollow.Parameters["CurrentWaypoint"]);
        Assert.Equal(new EnumValue("HLAfalse", 0), lFollow.Parameters["Loop"]);

        lCompleted = true;
        mSimulation.Step(sStep);
        Assert.Equal("Success", lFollow.Outcome);
    }

    [Fact]
    public void FireOnTarget_ActivatesOnConditionAndEndsOnEvent()
    {
        Entity lShooter = mFactory.SpawnEntity("Fighter");
        Entity lTarget = mFactory.SpawnEntity("Fighter");
        TaskInstance lFire = mTasks.Assign(mWorld, lShooter, "FireOnTarget", new Dictionary<string, DataValue> { ["Target"] = new ReferenceValue(lTarget) });
        bool lInRange = false;
        mTasks.RegisterCondition("InRange(Weapon, Target)", pContext => lInRange);

        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Pending, lFire.State);

        lInRange = true;
        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Running, lFire.State);
        Assert.Equal(new ReferenceValue(lShooter), lFire.Parameters["Shooter"]);

        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Running, lFire.State);

        mTasks.RaiseEvent(mWorld, lShooter, "CeaseFire");
        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Ended, lFire.State);
        Assert.Equal("Aborted", lFire.Outcome);
    }

    [Fact]
    public void ChangeFormation_OnUnit_UsesUnitDefaultsAndTimesOut()
    {
        Entity lPatrol = mFactory.SpawnUnit("FighterPatrol");

        TaskInstance lTask = mTasks.Assign(mWorld, lPatrol, "ChangeFormation");
        mSimulation.Run(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10));
        Assert.Equal(TaskState.Running, lTask.State);
        mSimulation.Step(TimeSpan.FromSeconds(10));

        // The delay runs from the start of the task, at the first step.
        Assert.Equal(TimeSpan.FromSeconds(10), lTask.StartedAt);
        Assert.Equal(new EnumValue("Line", 0), lTask.Parameters["Formation"]);
        Assert.Equal(new NumberValue(1000), lTask.Parameters["Spacing"]);
        Assert.Equal(new ReferenceValue(lPatrol), lTask.Parameters["Leader"]);
        Assert.Equal("Failure", lTask.Outcome);
    }

    [Fact]
    public void ChangeFormation_CustomTriggerReceivesArguments()
    {
        Entity lPatrol = mFactory.SpawnUnit("FighterPatrol");
        string? lTolerance = null;
        mTasks.RegisterCustomTrigger("FormationEstablished", (pContext, pTrigger) =>
        {
            lTolerance = pTrigger.Arguments.Single(pArgument => pArgument.Name == "tolerance").Value;
            return true;
        });

        TaskInstance lTask = mTasks.Assign(mWorld, lPatrol, "ChangeFormation", new Dictionary<string, DataValue> { ["Formation"] = new EnumValue("Wedge", 3) });
        mSimulation.Step(sStep);

        Assert.Equal("100", lTolerance);
        Assert.Equal("Success", lTask.Outcome);
        Assert.Equal(new EnumValue("Wedge", 3), lTask.Parameters["Formation"]);
    }

    private static Dictionary<string, DataValue> Destination(double pLatitude, double pLongitude)
    {
        return new Dictionary<string, DataValue> { ["Destination"] = TestLibrary.Point(pLatitude, pLongitude) };
    }

    private void RunUntil(Func<bool> pCondition, TimeSpan pTimeout)
    {
        while (!pCondition())
        {
            Assert.True(mSimulation.Elapsed < pTimeout, "The condition was not met in time.");
            mSimulation.Step(sStep);
        }
    }
}
