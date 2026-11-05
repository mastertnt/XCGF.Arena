using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace XCGF.Arena.Blueprints;

/// <summary>
/// On an entity: the graph that drives it.
/// </summary>
/// <param name="GraphId">Id of the graph in the runtime.</param>
public record struct BlueprintRef(int GraphId) : IComponent;

/// <summary>
/// State of a fiber. A fiber is an entity: one run of a graph, from an event to its end.
/// </summary>
/// <param name="Owner">Entity the graph is attached to.</param>
/// <param name="Key">The event that started the fiber.</param>
/// <param name="NodeId">Program counter: id of the current action, -1 once ended.</param>
/// <param name="Resuming">Whether the current action suspended the fiber and is to be resumed.</param>
public record struct FiberState(Entity Owner, EventKey Key, int NodeId, bool Resuming) : IComponent;

/// <summary>
/// On a fiber: asleep until a simulation time.
/// </summary>
/// <param name="WakeTime">The simulation time.</param>
public record struct FiberWait(TimeSpan WakeTime) : IComponent;

/// <summary>
/// On a fiber: ready to run.
/// </summary>
public struct FiberReady : ITag
{
}

/// <summary>
/// Runs the blueprint graphs attached to entities.
/// </summary>
/// <remarks>
/// At each step, <see cref="BlueprintSystem"/> starts a fiber for each event fired since the previous step, wakes the
/// fibers whose delay has elapsed, then runs the ready fibers until they wait, end, or exhaust their
/// <see cref="InstructionBudget"/>.
/// </remarks>
public sealed class BlueprintRuntime
{
    private readonly List<BlueprintGraph> mGraphs = new List<BlueprintGraph>();
    private readonly Queue<(Entity Owner, EventKey Key, object?[] Payload)> mPending = new Queue<(Entity Owner, EventKey Key, object?[] Payload)>();

    // Registers of the fibers, outside the ECS since their size depends on the graph.
    private readonly Dictionary<int, Dictionary<(int, int), object?>> mFiberData = new Dictionary<int, Dictionary<(int, int), object?>>();

    // Retrigger policies.
    private readonly Dictionary<EventKey, Entity> mRunning = new Dictionary<EventKey, Entity>();
    private readonly HashSet<EventKey> mFired = new HashSet<EventKey>();

    private readonly ArchetypeQuery<FiberState, FiberWait> mWaitingFibers;
    private readonly ArchetypeQuery<FiberState> mReadyFibers;
    private readonly List<Entity> mBuffer = new List<Entity>();

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintRuntime"/> class.
    /// </summary>
    /// <param name="pStore">The store holding the entities.</param>
    public BlueprintRuntime(EntityStore pStore)
    {
        ArgumentNullException.ThrowIfNull(pStore);
        Store = pStore;
        mWaitingFibers = pStore.Query<FiberState, FiberWait>();
        mReadyFibers = pStore.Query<FiberState>().AllTags(Tags.Get<FiberReady>());
    }

    /// <summary>
    /// Raised for each log line.
    /// </summary>
    public event Action<string>? Logged;

    /// <summary>
    /// Raised each time a node runs: an event fired, an action executed or resumed. For debuggers and viewers.
    /// </summary>
    public event Action<EventKey, int>? NodeExecuted;

    /// <summary>
    /// Gets the store holding the entities.
    /// </summary>
    public EntityStore Store { get; }

    /// <summary>
    /// Gets or sets the simulation time. <see cref="BlueprintSystem"/> sets it at each step.
    /// </summary>
    public TimeSpan SimTime { get; set; }

    /// <summary>
    /// Gets or sets the number of actions a fiber runs at most per step; a fiber that has more to run goes on at the next step.
    /// </summary>
    public int InstructionBudget { get; set; } = 64;

    /// <summary>
    /// Gets the registered graphs, by id.
    /// </summary>
    public IReadOnlyList<BlueprintGraph> Graphs => mGraphs;

    /// <summary>
    /// Registers a graph.
    /// </summary>
    /// <param name="pGraph">The graph.</param>
    /// <returns>The id of the graph.</returns>
    public int RegisterGraph(BlueprintGraph pGraph)
    {
        ArgumentNullException.ThrowIfNull(pGraph);
        mGraphs.Add(pGraph);
        return mGraphs.Count - 1;
    }

    /// <summary>
    /// Attaches a graph to an entity: each event node of the graph subscribes.
    /// </summary>
    /// <param name="pOwner">The entity.</param>
    /// <param name="pGraphId">The id of the graph.</param>
    public void Attach(Entity pOwner, int pGraphId)
    {
        BlueprintGraph lGraph = mGraphs[pGraphId];
        pOwner.AddComponent(new BlueprintRef(pGraphId));
        NodeContext lContext = new NodeContext(this, pOwner);
        Evaluator lEvaluator = new Evaluator(lGraph, lContext, new Dictionary<(int, int), object?>());
        foreach (NodeInstance lNode in lGraph.Nodes)
        {
            if (lNode.Definition is IEventNode lEvent)
            {
                lEvaluator.BeginInstruction();
                lEvent.Subscribe(lContext, lEvaluator.ArgsFor(lNode.Id), new EventKey(pOwner.Id, pGraphId, lNode.Id));
            }
        }
    }

    /// <summary>
    /// Fires an event: a fiber starts at the next step, according to the retrigger policy of the event.
    /// </summary>
    /// <param name="pOwner">The entity the graph is attached to.</param>
    /// <param name="pKey">The event, as given to <see cref="IEventNode.Subscribe"/>.</param>
    /// <param name="pPayload">The content of the firing, written to the outputs of the event node.</param>
    public void Trigger(Entity pOwner, EventKey pKey, object?[] pPayload) => mPending.Enqueue((pOwner, pKey, pPayload));

    /// <summary>
    /// Writes a log line, prefixed by the simulation time.
    /// </summary>
    /// <param name="pMessage">The message.</param>
    public void Log(string pMessage) => Logged?.Invoke(string.Create(CultureInfo.InvariantCulture, $"[t={SimTime.TotalSeconds,8:F2} s] {pMessage}"));

    /// <summary>
    /// Runs one step: fired events start fibers, elapsed delays wake fibers, ready fibers run.
    /// </summary>
    public void Update()
    {
        StartTriggeredFibers();
        WakeFibers();
        RunReadyFibers();
    }

    private void StartTriggeredFibers()
    {
        while (mPending.TryDequeue(out (Entity Owner, EventKey Key, object?[] Payload) lFiring))
        {
            BlueprintGraph lGraph = mGraphs[lFiring.Key.GraphId];
            IEventNode lEvent = (IEventNode)lGraph.Nodes[lFiring.Key.EventNode].Definition;
            switch (lEvent.Policy)
            {
                case RetriggerPolicy.Once when mFired.Contains(lFiring.Key):
                case RetriggerPolicy.Ignore when mRunning.ContainsKey(lFiring.Key):
                    Log($"#{lFiring.Owner.Id} {lEvent.TypeId} ignored ({lEvent.Policy})");
                    continue;
                case RetriggerPolicy.Restart when mRunning.TryGetValue(lFiring.Key, out Entity lOld):
                    EndFiber(lOld);
                    break;
            }

            mFired.Add(lFiring.Key);

            // The content of the firing becomes the outputs of the event node.
            Dictionary<(int, int), object?> lData = new Dictionary<(int, int), object?>();
            NodeContext lContext = new NodeContext(this, lFiring.Owner);
            lEvent.WritePayload(lFiring.Payload, new Evaluator(lGraph, lContext, lData).ArgsFor(lFiring.Key.EventNode));

            Entity lFiber = Store.CreateEntity(new FiberState(lFiring.Owner, lFiring.Key, lGraph.NextExec(lFiring.Key.EventNode, 0), false));
            lFiber.AddTag<FiberReady>();
            mFiberData[lFiber.Id] = lData;
            if (lEvent.Policy != RetriggerPolicy.Parallel)
            {
                mRunning[lFiring.Key] = lFiber;
            }

            Log($"#{lFiring.Owner.Id} {lEvent.TypeId} -> fiber #{lFiber.Id}");
            NodeExecuted?.Invoke(lFiring.Key, lFiring.Key.EventNode);
        }
    }

    private void WakeFibers()
    {
        mBuffer.Clear();
        foreach (Entity lFiber in mWaitingFibers.Entities)
        {
            if (SimTime >= lFiber.GetComponent<FiberWait>().WakeTime)
            {
                mBuffer.Add(lFiber);
            }
        }

        // Structural changes, outside the query iteration.
        foreach (Entity lFiber in mBuffer)
        {
            lFiber.RemoveComponent<FiberWait>();
            lFiber.AddTag<FiberReady>();
        }
    }

    private void RunReadyFibers()
    {
        mBuffer.Clear();
        foreach (Entity lFiber in mReadyFibers.Entities)
        {
            mBuffer.Add(lFiber);
        }

        foreach (Entity lFiber in mBuffer)
        {
            RunFiber(lFiber);
        }
    }

    private void RunFiber(Entity pFiber)
    {
        FiberState lState = pFiber.GetComponent<FiberState>();
        if (lState.Owner.IsNull)
        {
            EndFiber(pFiber);
            return;
        }

        BlueprintGraph lGraph = mGraphs[lState.Key.GraphId];
        NodeContext lContext = new NodeContext(this, lState.Owner);
        Evaluator lEvaluator = new Evaluator(lGraph, lContext, mFiberData[pFiber.Id]);
        for (int lBudget = InstructionBudget; lBudget > 0 && lState.NodeId >= 0; lBudget--)
        {
            IActionNode lAction = (IActionNode)lGraph.Nodes[lState.NodeId].Definition;
            lEvaluator.BeginInstruction();
            NodeArgs lArgs = lEvaluator.ArgsFor(lState.NodeId);
            NodeExecuted?.Invoke(lState.Key, lState.NodeId);
            ExecResult lResult = lState.Resuming ? lAction.Resume(lContext, lArgs) : lAction.Execute(lContext, lArgs);
            lState.Resuming = false;
            switch (lResult.Kind)
            {
                case ExecKind.Next:
                    lState.NodeId = lGraph.NextExec(lState.NodeId, lResult.ExecPin);
                    break;
                case ExecKind.End:
                    lState.NodeId = -1;
                    break;
                case ExecKind.Wait:
                    lState.Resuming = true;
                    pFiber.GetComponent<FiberState>() = lState;
                    pFiber.RemoveTag<FiberReady>();
                    pFiber.AddComponent(new FiberWait(lResult.WakeTime));
                    return;
            }
        }

        if (lState.NodeId < 0)
        {
            EndFiber(pFiber);
            return;
        }

        // Budget exhausted: the fiber goes on at the next step.
        pFiber.GetComponent<FiberState>() = lState;
    }

    private void EndFiber(Entity pFiber)
    {
        FiberState lState = pFiber.GetComponent<FiberState>();
        if (mRunning.TryGetValue(lState.Key, out Entity lRunning) && lRunning.Id == pFiber.Id)
        {
            mRunning.Remove(lState.Key);
        }

        mFiberData.Remove(pFiber.Id);
        Log($"#{lState.Owner.Id} fiber #{pFiber.Id} ended");
        pFiber.DeleteEntity();
    }

    private sealed class NodeContext : IExecContext
    {
        private readonly BlueprintRuntime mRuntime;

        public NodeContext(BlueprintRuntime pRuntime, Entity pSelf)
        {
            mRuntime = pRuntime;
            Self = pSelf;
        }

        public BlueprintRuntime Runtime => mRuntime;

        public EntityStore Store => mRuntime.Store;

        public Entity Self { get; }

        public TimeSpan SimTime => mRuntime.SimTime;

        public void Log(string pMessage) => mRuntime.Log($"#{Self.Id} {pMessage}");
    }
}

/// <summary>
/// Runs a <see cref="BlueprintRuntime"/> at each step, at the simulation time of the step.
/// </summary>
public sealed class BlueprintSystem : QuerySystem<BlueprintRef>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintSystem"/> class.
    /// </summary>
    /// <param name="pRuntime">The runtime.</param>
    public BlueprintSystem(BlueprintRuntime pRuntime)
    {
        ArgumentNullException.ThrowIfNull(pRuntime);
        Runtime = pRuntime;
    }

    /// <summary>
    /// Gets the runtime.
    /// </summary>
    public BlueprintRuntime Runtime { get; }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        Runtime.SimTime = TimeSpan.FromSeconds(Tick.time);
        Runtime.Update();
    }
}
