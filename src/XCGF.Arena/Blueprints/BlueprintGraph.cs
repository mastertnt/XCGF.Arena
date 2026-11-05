namespace XCGF.Arena.Blueprints;

/// <summary>
/// Node placed in a graph, with the constant values of its data inputs.
/// </summary>
/// <param name="Id">Id of the node in its graph.</param>
/// <param name="Definition">Kind of node.</param>
/// <param name="Constants">Values of the data inputs that are not linked, by input pin.</param>
public sealed record NodeInstance(int Id, IBlueprintNode Definition, object?[] Constants);

/// <summary>
/// Graph of nodes linked by exec links (the order actions run in) and data links (where inputs read their values).
/// </summary>
/// <remarks>
/// A graph is pure data, shared by every entity it is attached to. It is interpreted as is: the program counter of a
/// fiber is the id of its current node.
/// </remarks>
public sealed class BlueprintGraph
{
    private readonly List<NodeInstance> mNodes = new List<NodeInstance>();
    private readonly Dictionary<(int Node, int Pin), int> mExec = new Dictionary<(int Node, int Pin), int>();
    private readonly Dictionary<(int Node, int Pin), (int Node, int Pin)> mData = new Dictionary<(int Node, int Pin), (int Node, int Pin)>();

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintGraph"/> class.
    /// </summary>
    /// <param name="pName">Name of the graph.</param>
    public BlueprintGraph(string pName)
    {
        Name = pName;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the nodes, by id.
    /// </summary>
    public IReadOnlyList<NodeInstance> Nodes => mNodes;

    /// <summary>
    /// Gets the editor position of the nodes, by id. The runtime does not use it.
    /// </summary>
    public Dictionary<int, (double X, double Y)> Layout { get; } = new Dictionary<int, (double X, double Y)>();

    /// <summary>
    /// Gets the exec links.
    /// </summary>
    public IEnumerable<(int From, int FromPin, int To)> ExecLinks => mExec.Select(pLink => (pLink.Key.Node, pLink.Key.Pin, pLink.Value));

    /// <summary>
    /// Gets the data links.
    /// </summary>
    public IEnumerable<(int From, int FromPin, int To, int ToPin)> DataLinks => mData.Select(pLink => (pLink.Value.Node, pLink.Value.Pin, pLink.Key.Node, pLink.Key.Pin));

    /// <summary>
    /// Adds a node.
    /// </summary>
    /// <param name="pDefinition">Kind of node.</param>
    /// <param name="pConstants">Values of its first data inputs, used when they are not linked.</param>
    /// <returns>The id of the node.</returns>
    public int Add(IBlueprintNode pDefinition, params object?[] pConstants)
    {
        ArgumentNullException.ThrowIfNull(pDefinition);
        if (pConstants.Length > pDefinition.Inputs.Length)
        {
            throw new ArgumentException($"{pDefinition.TypeId} has {pDefinition.Inputs.Length} inputs, not {pConstants.Length}.", nameof(pConstants));
        }

        object?[] lValues = new object?[pDefinition.Inputs.Length];
        Array.Copy(pConstants, lValues, pConstants.Length);
        mNodes.Add(new NodeInstance(mNodes.Count, pDefinition, lValues));
        return mNodes.Count - 1;
    }

    /// <summary>
    /// Links an exec output of a node to the exec input of an action.
    /// </summary>
    /// <param name="pFrom">The node that runs first: an event or an action.</param>
    /// <param name="pTo">The action that runs next.</param>
    /// <param name="pFromPin">The exec output of the first node.</param>
    public void Exec(int pFrom, int pTo, int pFromPin = 0)
    {
        if (mNodes[pFrom].Definition is IPureNode)
        {
            throw new InvalidOperationException($"{mNodes[pFrom].Definition.TypeId} is pure: it has no exec output.");
        }

        if (mNodes[pTo].Definition is not IActionNode)
        {
            throw new InvalidOperationException($"{mNodes[pTo].Definition.TypeId} has no exec input.");
        }

        mExec[(pFrom, pFromPin)] = pTo;
    }

    /// <summary>
    /// Links a data output of a node to a data input of another one.
    /// </summary>
    /// <param name="pFrom">The node giving the value.</param>
    /// <param name="pFromPin">Its output.</param>
    /// <param name="pTo">The node reading the value.</param>
    /// <param name="pToPin">Its input.</param>
    public void Data(int pFrom, int pFromPin, int pTo, int pToPin)
    {
        PinType lOutput = mNodes[pFrom].Definition.Outputs[pFromPin].Type;
        PinType lInput = mNodes[pTo].Definition.Inputs[pToPin].Type;
        if (lOutput != lInput)
        {
            throw new InvalidOperationException($"A {lOutput} output cannot feed a {lInput} input.");
        }

        mData[(pTo, pToPin)] = (pFrom, pFromPin);
    }

    /// <summary>
    /// Finds the action an exec output leads to.
    /// </summary>
    /// <param name="pNode">The node.</param>
    /// <param name="pPin">Its exec output.</param>
    /// <returns>The id of the action, or -1 when the output is not linked.</returns>
    public int NextExec(int pNode, int pPin) => mExec.TryGetValue((pNode, pPin), out int lTarget) ? lTarget : -1;

    /// <summary>
    /// Finds the output a data input is linked to.
    /// </summary>
    /// <param name="pNode">The node.</param>
    /// <param name="pPin">Its data input.</param>
    /// <param name="pSource">The node and output it reads.</param>
    /// <returns>True when the input is linked.</returns>
    public bool TryGetSource(int pNode, int pPin, out (int Node, int Pin) pSource) => mData.TryGetValue((pNode, pPin), out pSource);
}

/// <summary>
/// Access of a node to its data pins.
/// </summary>
public readonly struct NodeArgs
{
    private readonly Evaluator mEvaluator;
    private readonly int mNode;
    private readonly Dictionary<(int, int), object?> mTarget;

    internal NodeArgs(Evaluator pEvaluator, int pNode, Dictionary<(int, int), object?> pTarget)
    {
        mEvaluator = pEvaluator;
        mNode = pNode;
        mTarget = pTarget;
    }

    /// <summary>
    /// Reads a data input: the output it is linked to, or else its constant.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="pPin">The input.</param>
    /// <returns>The value.</returns>
    public T In<T>(int pPin)
    {
        object? lValue = mEvaluator.ReadInput(mNode, pPin);
        return lValue is T lTyped ? lTyped : (T)Convert.ChangeType(lValue!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a data output.
    /// </summary>
    /// <param name="pPin">The output.</param>
    /// <param name="pValue">The value.</param>
    public void Out(int pPin, object? pValue) => mTarget[(mNode, pPin)] = pValue;
}

/// <summary>
/// Resolves the data inputs. A pure node is evaluated on demand, at most once per instruction; an output of an action or
/// an event is read from the registers of the fiber.
/// </summary>
internal sealed class Evaluator
{
    private readonly BlueprintGraph mGraph;
    private readonly IReadContext mContext;
    private readonly Dictionary<(int, int), object?> mFiberOutputs;
    private readonly Dictionary<(int, int), object?> mPureCache = new Dictionary<(int, int), object?>();

    public Evaluator(BlueprintGraph pGraph, IReadContext pContext, Dictionary<(int, int), object?> pFiberOutputs)
    {
        mGraph = pGraph;
        mContext = pContext;
        mFiberOutputs = pFiberOutputs;
    }

    public void BeginInstruction() => mPureCache.Clear();

    public NodeArgs ArgsFor(int pNode) => new NodeArgs(this, pNode, mFiberOutputs);

    internal object? ReadInput(int pNode, int pPin)
    {
        if (!mGraph.TryGetSource(pNode, pPin, out (int Node, int Pin) lSource))
        {
            return mGraph.Nodes[pNode].Constants[pPin];
        }

        if (mGraph.Nodes[lSource.Node].Definition is IPureNode lPure)
        {
            if (!mPureCache.TryGetValue(lSource, out object? lCached))
            {
                lPure.Evaluate(mContext, new NodeArgs(this, lSource.Node, mPureCache));
                lCached = mPureCache.GetValueOrDefault(lSource);
            }

            return lCached;
        }

        return mFiberOutputs.GetValueOrDefault(lSource);
    }
}
