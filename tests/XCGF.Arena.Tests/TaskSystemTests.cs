using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Tests;

public class TaskSystemTests
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    private readonly ArenaSimulation mSimulation;
    private readonly Spawning.ArchetypeFactory mFactory;
    private readonly TaskSystem mTasks;

    public TaskSystemTests()
    {
        mSimulation = new ArenaSimulation(TestLibrary.Example);
        mFactory = mSimulation.Factory;
        mTasks = mSimulation.Tasks;
    }

    [Fact]
    public void Assign_FillsDefaultsAndDefaultsTheTaskeeToTheCarrier()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        TaskInstance lTask = mTasks.Assign(lFighter, "MoveTo", Destination(0, 0.1));

        Assert.Equal(TaskState.Pending, lTask.State);
        Assert.Equal(new EnumValue("Cruise", 1), lTask.Parameters["Throttle"]);
        Assert.Null(lTask.Parameters["Speed"]);
        Assert.Equal(new ReferenceValue(lFighter), lTask.Parameters["Executor"]);
        Assert.Null(lTask.Parameters["TravelTime"]);
        Assert.Same(lTask, lFighter.GetComponent<AssignedTasks>().Items.Single());
    }

    [Fact]
    public void Assign_WithoutRequiredParameter_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mTasks.Assign(lFighter, "MoveTo"));

        Assert.Contains("Required parameter 'Destination' is missing", lError.Message);
    }

    [Fact]
    public void Assign_TaskTheCarrierCannotPerform_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        Assert.Throws<XcgfDataException>(() => mTasks.Assign(lFighter, "ChangeFormation"));
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

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mTasks.Assign(lFighter, "FireOnTarget", lValues));

        Assert.Contains("'Salvo' = 3 must be <= 2", lError.Message);
    }

    [Fact]
    public void Assign_SpeedOutsideTheFlightEnvelope_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lValues = new Dictionary<string, DataValue>(Destination(0, 1)) { ["Speed"] = new NumberValue(700) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mTasks.Assign(lFighter, "MoveTo", lValues));

        // The bound comes from the TaskRef of FixedWingKinematic, read against the data of the fighter's kinematic.
        Assert.Contains("'Speed' = 700 must be <= 590 (MaxSpeed)", lError.Message);
    }

    [Fact]
    public void Assign_OutParameterOrUnknownParameter_Throws()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lOut = new Dictionary<string, DataValue>(Destination(0, 1)) { ["TravelTime"] = new NumberValue(1) };
        Dictionary<string, DataValue> lUnknown = new Dictionary<string, DataValue>(Destination(0, 1)) { ["Altitude"] = new NumberValue(1) };

        Assert.Throws<XcgfDataException>(() => mTasks.Assign(lFighter, "MoveTo", lOut));
        Assert.Throws<XcgfDataException>(() => mTasks.Assign(lFighter, "MoveTo", lUnknown));
    }

    [Fact]
    public void MoveTo_FliesToDestinationAtCruiseSpeedAndSucceeds()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        TaskInstance lTask = mTasks.Assign(lFighter, "MoveTo", Destination(0.1, 0.1));
        double lDistance = GeoMath.Distance(default, new GeoPosition(0.1, 0.1, 0));

        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Running, lTask.State);
        Assert.Equal(45, lFighter.GetComponent<Kinematic>().Orientation.Heading, 0);

        // Cruise row of the FighterKinematic performance table, for the 0 to 2000 m band.
        Assert.Equal(230, lFighter.GetComponent<Kinematic>().Speed);

        RunUntil(() => lTask.State == TaskState.Ended, TimeSpan.FromHours(1));

        Assert.Equal("Success", lTask.Outcome);
        Assert.True(GeoMath.Distance(lFighter.GetComponent<Kinematic>().Position, new GeoPosition(0.1, 0.1, 0)) <= 500);
        double lTravelTime = lTask.Parameters["TravelTime"]!.AsNumber()!.Value;
        Assert.InRange(lTravelTime, (lDistance - 500) / 230 - 2, (lDistance / 230) + 2);
    }

    [Fact]
    public void MoveTo_ClimbsToTheDestinationAltitudeWithinTheClimbRate()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lValues = new Dictionary<string, DataValue>
        {
            ["Destination"] = new GeoPosition(0, 2, 3000).ToValue(),
            ["Speed"] = new NumberValue(300),
        };
        mTasks.Assign(lFighter, "MoveTo", lValues);
        double lClimbRate = lFighter.FindEquipment("Kinematic").GetComponent<EquipmentData>().Technological.GetNumber("ClimbRate")!.Value;

        mSimulation.Step(sStep);
        Assert.InRange(lFighter.GetComponent<Kinematic>().Position.Altitude, 1, lClimbRate + 1);

        mSimulation.Run(TimeSpan.FromMinutes(3), sStep);
        Assert.InRange(lFighter.GetComponent<Kinematic>().Position.Altitude, 2990, 3010);
        Assert.Equal(300, lFighter.GetComponent<Kinematic>().Speed);
    }

    [Fact]
    public void MoveTo_OrderCancelled_Aborts()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        TaskInstance lTask = mTasks.Assign(lFighter, "MoveTo", Destination(0, 179));
        mSimulation.Step(sStep);

        mTasks.RaiseEvent(lFighter, "OrderCancelled");
        mSimulation.Step(sStep);

        Assert.Equal(TaskState.Ended, lTask.State);
        Assert.Equal("Aborted", lTask.Outcome);
    }

    [Fact]
    public void FollowCourse_GoesThroughTheWaypointsInOrderAndCompletes()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lCourse = new Dictionary<string, DataValue>
        {
            ["Waypoints"] = new ArrayValue(new DataValue[]
            {
                RecordValue.Of(("Position", new GeoPosition(0, 0.05, 0).ToValue()), ("Speed", new NumberValue(300))),
                RecordValue.Of(("Position", new GeoPosition(0.05, 0.05, 0).ToValue())),
            }),
        };
        TaskInstance lTask = mTasks.Assign(lFighter, "FollowCourse", lCourse);

        mSimulation.Step(sStep);
        Assert.Equal(90, lFighter.GetComponent<Kinematic>().Orientation.Heading, 0);
        Assert.Equal(300, lFighter.GetComponent<Kinematic>().Speed);

        RunUntil(() => lTask.Parameters["CurrentWaypoint"] is IntegerValue { Value: 1 }, TimeSpan.FromMinutes(10));
        mSimulation.Step(sStep);

        // Heading north, give or take the 500 m tolerance of the first waypoint.
        double lHeading = lFighter.GetComponent<Kinematic>().Orientation.Heading;
        Assert.InRange(lHeading < 180 ? lHeading : lHeading - 360, -5, 5);

        // The second leg gives no speed: the Cruise throttle of the task applies.
        Assert.Equal(230, lFighter.GetComponent<Kinematic>().Speed);

        RunUntil(() => lTask.State == TaskState.Ended, TimeSpan.FromMinutes(20));
        Assert.Equal("Success", lTask.Outcome);
        Assert.True(GeoMath.Distance(lFighter.GetComponent<Kinematic>().Position, new GeoPosition(0.05, 0.05, 0)) <= 500);
    }

    [Fact]
    public void FollowCourse_WithLoop_RestartsFromTheFirstWaypoint()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        Dictionary<string, DataValue> lCourse = new Dictionary<string, DataValue>
        {
            ["Waypoints"] = new ArrayValue(new DataValue[] { RecordValue.Of(("Position", new GeoPosition(0, 0.02, 0).ToValue())) }),
            ["Loop"] = new EnumValue("HLAtrue", 1),
        };
        TaskInstance lTask = mTasks.Assign(lFighter, "FollowCourse", lCourse);

        mSimulation.Run(TimeSpan.FromMinutes(5), sStep);

        Assert.Equal(TaskState.Running, lTask.State);
        Assert.Equal(new IntegerValue(0), lTask.Parameters["CurrentWaypoint"]);
    }

    [Fact]
    public void HoldPosition_CirclesAroundTheHoldPointUntilTheDurationElapses()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");
        TaskInstance lTask = mTasks.Assign(lFighter, "HoldPosition", new Dictionary<string, DataValue> { ["Duration"] = new NumberValue(300) });
        double lFarthest = 0;

        mSimulation.Step(sStep);
        GeoPosition lHoldPoint = GeoPosition.FromValue(lTask.Parameters["HoldPoint"]!);
        Assert.Equal(0, lHoldPoint.Latitude, 6);
        RunUntil(
            () =>
            {
                lFarthest = Math.Max(lFarthest, GeoMath.Distance(lFighter.GetComponent<Kinematic>().Position, lHoldPoint));
                return lTask.State == TaskState.Ended;
            },
            TimeSpan.FromMinutes(10));

        // A fighter cannot stop: it loiters at 110 m/s and stays close to the point.
        Assert.Equal(110, lFighter.GetComponent<Kinematic>().Speed);
        Assert.InRange(lFarthest, 1, 1000);
        Assert.Equal("Success", lTask.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(301), lTask.EndedAt);
    }

    [Fact]
    public void FireOnTarget_ActivatesOnConditionAndEndsOnEvent()
    {
        Entity lShooter = mFactory.SpawnEntity("Fighter");
        Entity lTarget = mFactory.SpawnEntity("Fighter");
        TaskInstance lFire = mTasks.Assign(lShooter, "FireOnTarget", new Dictionary<string, DataValue> { ["Target"] = new ReferenceValue(lTarget) });
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

        mTasks.RaiseEvent(lShooter, "CeaseFire");
        mSimulation.Step(sStep);
        Assert.Equal(TaskState.Ended, lFire.State);
        Assert.Equal("Aborted", lFire.Outcome);
    }

    [Fact]
    public void ChangeFormation_OnUnit_UsesUnitDefaultsAndTimesOut()
    {
        Entity lPatrol = mFactory.SpawnUnit("FighterPatrol");

        TaskInstance lTask = mTasks.Assign(lPatrol, "ChangeFormation");
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

        TaskInstance lTask = mTasks.Assign(lPatrol, "ChangeFormation", new Dictionary<string, DataValue> { ["Formation"] = new EnumValue("Wedge", 3) });
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
