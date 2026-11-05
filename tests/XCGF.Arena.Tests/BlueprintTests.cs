using Friflo.Engine.ECS;
using XCGF.Arena.Blueprints;
using XCGF.Arena.Blueprints.Nodes;
using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Tests;

public class BlueprintTests
{
    private readonly EntityStore mStore = new EntityStore();
    private readonly BlueprintRuntime mRuntime;
    private readonly List<string> mLog = new List<string>();

    public BlueprintTests()
    {
        mRuntime = new BlueprintRuntime(mStore);
        mRuntime.Logged += mLog.Add;
    }

    [Fact]
    public void RadarPatrol_ReactsToZoneEntryWithDelaysAndPureNodes()
    {
        ArenaSimulation lSimulation = new ArenaSimulation(TestLibrary.Example);
        List<string> lLog = new List<string>();
        lSimulation.Blueprints.Logged += lLog.Add;
        Entity lFighter = lSimulation.Factory.SpawnEntity("Fighter", new Dictionary<string, DataValue>
        {
            ["Kinematic.Position"] = new GeoPosition(0, 0, 5000).ToValue(),
            ["Kinematic.Orientation"] = new Orientation(0, 0, 0).ToValue(),
        });
        lFighter.AddComponent(new KinematicLimits(null, null, null));
        lFighter.GetComponent<Kinematic>().Speed = 0;
        Entity lZone = lSimulation.Store.CreateEntity(new CircularZone(GeoMath.Offset(new GeoPosition(0, 0, 0), 0, 20000), 5000));

        // [OnStart] -> [SetSpeed 200]
        // [OnZoneEnter] -> [Radar.Mode = Off] -> [Delay 30] -> [Radar.Mode = Search] -> [Delay (90 - 30)]
        //    -> [SetHeading AddHeading(GetHeading(Self), 180)]
        BlueprintGraph lGraph = new BlueprintGraph("radar-patrol");
        int lStart = lGraph.Add(new OnStart());
        int lSpeed = lGraph.Add(new SetSpeed(), 200.0);
        lGraph.Exec(lStart, lSpeed);

        SetEquipmentParameter lSetParameter = new SetEquipmentParameter(lSimulation.Library);
        int lEnter = lGraph.Add(new OnZoneEnter(), lZone);
        int lRadarOff = lGraph.Add(lSetParameter, "Radar", "Mode", "Off");
        int lDelayX = lGraph.Add(new Delay(), 30.0);
        int lRadarOn = lGraph.Add(lSetParameter, "Radar", "Mode", "Search");
        int lDelayYX = lGraph.Add(new Delay());
        int lTurn = lGraph.Add(new SetHeading());
        lGraph.Exec(lEnter, lRadarOff);
        lGraph.Exec(lRadarOff, lDelayX);
        lGraph.Exec(lDelayX, lRadarOn);
        lGraph.Exec(lRadarOn, lDelayYX);
        lGraph.Exec(lDelayYX, lTurn);

        int lYMinusX = lGraph.Add(new Subtract(), 90.0, 30.0);
        int lSelf = lGraph.Add(new Self());
        int lHeading = lGraph.Add(new GetHeading());
        int lPlus180 = lGraph.Add(new AddHeading(), 0.0, 180.0);
        lGraph.Data(lYMinusX, 0, lDelayYX, 0);
        lGraph.Data(lSelf, 0, lHeading, 0);
        lGraph.Data(lHeading, 0, lPlus180, 0);
        lGraph.Data(lPlus180, 0, lTurn, 0);

        lSimulation.Blueprints.Attach(lFighter, lSimulation.Blueprints.RegisterGraph(lGraph));

        // 15 km to the zone at 200 m/s: entry after about 76 s.
        lSimulation.Run(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(1));
        Assert.Equal(200, lFighter.GetComponent<Kinematic>().Speed);
        Assert.Equal("Off", RadarMode(lFighter));
        Assert.Contains(lLog, pLine => pLine.Contains("enters zone", StringComparison.Ordinal));

        lSimulation.Run(TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(1));
        Assert.Equal("Search", RadarMode(lFighter));
        Assert.Equal(0, lFighter.GetComponent<Kinematic>().Orientation.Heading, 3);

        lSimulation.Run(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(1));
        Assert.Equal(180, lFighter.GetComponent<Kinematic>().Orientation.Heading, 3);
        Assert.Equal(0, lSimulation.Store.Query<FiberState>().Count);
    }

    [Theory]
    [InlineData(RetriggerPolicy.Parallel, 2)]
    [InlineData(RetriggerPolicy.Ignore, 1)]
    [InlineData(RetriggerPolicy.Restart, 1)]
    [InlineData(RetriggerPolicy.Once, 1)]
    public void Trigger_WhileAFiberRuns_FollowsTheRetriggerPolicy(RetriggerPolicy pPolicy, int pFibers)
    {
        ManualEvent lEvent = new ManualEvent(pPolicy);
        BlueprintGraph lGraph = new BlueprintGraph("retrigger");
        int lEventNode = lGraph.Add(lEvent);
        int lDelay = lGraph.Add(new Delay(), 10.0);
        lGraph.Exec(lEventNode, lDelay);
        Entity lOwner = mStore.CreateEntity();
        mRuntime.Attach(lOwner, mRuntime.RegisterGraph(lGraph));

        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), Array.Empty<object?>());
        Step(1);
        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), Array.Empty<object?>());
        Step(2);

        Assert.Equal(pFibers, mStore.Query<FiberState>().Count);
    }

    [Fact]
    public void Trigger_WithOncePolicy_IgnoresFiringsAfterTheFirstFiberEnded()
    {
        ManualEvent lEvent = new ManualEvent(RetriggerPolicy.Once);
        BlueprintGraph lGraph = new BlueprintGraph("once");
        int lEventNode = lGraph.Add(lEvent);
        lGraph.Exec(lEventNode, lGraph.Add(new LogMessage(), "fired"));
        Entity lOwner = mStore.CreateEntity();
        mRuntime.Attach(lOwner, mRuntime.RegisterGraph(lGraph));

        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), Array.Empty<object?>());
        Step(1);
        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), Array.Empty<object?>());
        Step(2);

        Assert.Single(mLog, pLine => pLine.EndsWith("fired", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(5.0, "true branch", "false branch")]
    [InlineData(1.0, "false branch", "true branch")]
    public void Branch_FollowsTheExecOutputOfItsCondition(double pValue, string pExpected, string pOther)
    {
        ManualEvent lEvent = new ManualEvent(RetriggerPolicy.Parallel);
        BlueprintGraph lGraph = new BlueprintGraph("branch");
        int lEventNode = lGraph.Add(lEvent);
        int lBranch = lGraph.Add(new Branch());
        int lCompare = lGraph.Add(new GreaterThan(), pValue, 3.0);
        lGraph.Exec(lEventNode, lBranch);
        lGraph.Data(lCompare, 0, lBranch, 0);
        lGraph.Exec(lBranch, lGraph.Add(new LogMessage(), "true branch"), Branch.TruePin);
        lGraph.Exec(lBranch, lGraph.Add(new LogMessage(), "false branch"), Branch.FalsePin);
        Entity lOwner = mStore.CreateEntity();
        mRuntime.Attach(lOwner, mRuntime.RegisterGraph(lGraph));

        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), Array.Empty<object?>());
        Step(1);

        Assert.Single(mLog, pLine => pLine.EndsWith(pExpected, StringComparison.Ordinal));
        Assert.DoesNotContain(mLog, pLine => pLine.EndsWith(pOther, StringComparison.Ordinal));
    }

    [Fact]
    public void Update_StopsAFiberAtItsInstructionBudgetAndGoesOnAtTheNextStep()
    {
        ManualEvent lEvent = new ManualEvent(RetriggerPolicy.Parallel);
        BlueprintGraph lGraph = new BlueprintGraph("budget");
        int lPrevious = lGraph.Add(lEvent);
        for (int lIndex = 0; lIndex < 3; lIndex++)
        {
            int lLog = lGraph.Add(new LogMessage(), $"line {lIndex}");
            lGraph.Exec(lPrevious, lLog);
            lPrevious = lLog;
        }

        Entity lOwner = mStore.CreateEntity();
        mRuntime.Attach(lOwner, mRuntime.RegisterGraph(lGraph));
        mRuntime.InstructionBudget = 2;

        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), Array.Empty<object?>());
        Step(1);
        Assert.Equal(2, mLog.Count(pLine => pLine.Contains(" line ", StringComparison.Ordinal)));
        Assert.Equal(1, mStore.Query<FiberState>().Count);

        Step(2);
        Assert.Equal(3, mLog.Count(pLine => pLine.Contains(" line ", StringComparison.Ordinal)));
        Assert.Equal(0, mStore.Query<FiberState>().Count);
    }

    [Fact]
    public void EventPayload_FeedsTheDataInputsOfTheFiber()
    {
        ManualEvent lEvent = new ManualEvent(RetriggerPolicy.Parallel);
        BlueprintGraph lGraph = new BlueprintGraph("payload");
        int lEventNode = lGraph.Add(lEvent);
        int lDelay = lGraph.Add(new Delay());
        int lLog = lGraph.Add(new LogMessage(), "woke up");
        lGraph.Exec(lEventNode, lDelay);
        lGraph.Exec(lDelay, lLog);
        lGraph.Data(lEventNode, 0, lDelay, 0);
        Entity lOwner = mStore.CreateEntity();
        mRuntime.Attach(lOwner, mRuntime.RegisterGraph(lGraph));

        mRuntime.Trigger(lOwner, lEvent.Keys.Single(), new object?[] { 5.0 });
        Step(1);
        Step(5);
        Assert.DoesNotContain(mLog, pLine => pLine.EndsWith("woke up", StringComparison.Ordinal));
        Step(6);
        Assert.Contains(mLog, pLine => pLine.EndsWith("woke up", StringComparison.Ordinal));
    }

    [Fact]
    public void Data_RejectsPinsOfDifferentTypes()
    {
        BlueprintGraph lGraph = new BlueprintGraph("types");
        int lSelf = lGraph.Add(new Self());
        int lDelay = lGraph.Add(new Delay());

        Assert.Throws<InvalidOperationException>(() => lGraph.Data(lSelf, 0, lDelay, 0));
    }

    [Fact]
    public void Exec_RejectsPureNodes()
    {
        BlueprintGraph lGraph = new BlueprintGraph("exec");
        int lSelf = lGraph.Add(new Self());
        int lDelay = lGraph.Add(new Delay());

        Assert.Throws<InvalidOperationException>(() => lGraph.Exec(lSelf, lDelay));
        Assert.Throws<InvalidOperationException>(() => lGraph.Exec(lDelay, lSelf));
    }

    private static string? RadarMode(Entity pFighter)
    {
        return (pFighter.FindEquipment("Radar").GetComponent<EquipmentData>().Initialization.Get("Mode") as EnumValue)?.Name;
    }

    private void Step(double pSeconds)
    {
        mRuntime.SimTime = TimeSpan.FromSeconds(pSeconds);
        mRuntime.Update();
    }

    /// <summary>
    /// Event fired by the test itself. Its payload is a number.
    /// </summary>
    private sealed class ManualEvent : IEventNode
    {
        public ManualEvent(RetriggerPolicy pPolicy)
        {
            Policy = pPolicy;
        }

        public string TypeId => "Test.Manual";

        public PinDef[] Inputs { get; } = Array.Empty<PinDef>();

        public PinDef[] Outputs { get; } = new[] { new PinDef("Value", PinType.Number) };

        public RetriggerPolicy Policy { get; }

        public List<EventKey> Keys { get; } = new List<EventKey>();

        public void Subscribe(IExecContext pContext, NodeArgs pParameters, EventKey pKey) => Keys.Add(pKey);

        public void WritePayload(object?[] pPayload, NodeArgs pArgs)
        {
            if (pPayload.Length > 0)
            {
                pArgs.Out(0, pPayload[0]);
            }
        }
    }
}
