using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Friflo.Engine.ECS;
using NodeEditor.Model;
using NodeEditor.Mvvm;
using XCGF.Arena.Blueprints;
using XCGF.Arena.Blueprints.Nodes;
using XCGF.Arena.Components;

namespace XCGF.Arena.Map.Blueprints;

/// <summary>
/// Drawing of the graph attached to an entity, for the node editor: nodes, pins (named "exec" or after their type) and links, then, at each refresh, the
/// fibers waiting on each node and a halo on the nodes just run.
/// </summary>
public sealed class BlueprintDrawing
{
    private const double NodeWidth = 210;
    private const double Header = 24;
    private const double RowHeight = 19;
    private const double PinSize = 11;
    private const double Margin = 60;

    private static readonly TimeSpan sFlashDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan sShownWait = TimeSpan.FromSeconds(0.5);

    private readonly BlueprintGraph mGraph;
    private readonly Dictionary<int, NodeContent> mContents = new Dictionary<int, NodeContent>();
    private readonly Dictionary<int, DateTime> mFlashes = new Dictionary<int, DateTime>();

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintDrawing"/> class.
    /// </summary>
    /// <param name="pOwner">The entity the graph is attached to.</param>
    /// <param name="pGraphId">The id of the graph in the runtime.</param>
    /// <param name="pGraph">The graph.</param>
    public BlueprintDrawing(Entity pOwner, int pGraphId, BlueprintGraph pGraph)
    {
        ArgumentNullException.ThrowIfNull(pGraph);
        Owner = pOwner;
        GraphId = pGraphId;
        mGraph = pGraph;
        string lCallsign = pOwner.TryGetComponent(out Force lForce) ? lForce.Callsign : $"#{pOwner.Id}";
        Label = $"{lCallsign} · {pGraph.Name}";

        ObservableCollection<INode> lNodes = new ObservableCollection<INode>();
        ObservableCollection<IConnector> lConnectors = new ObservableCollection<IConnector>();
        Drawing = new DrawingNodeViewModel
        {
            Name = pGraph.Name,
            X = 0,
            Y = 0,
            Nodes = lNodes,
            Connectors = lConnectors,
            Settings = new DrawingNodeSettingsViewModel
            {
                EnableConnections = false,
                EnableGrid = true,
                GridCellWidth = 20,
                GridCellHeight = 20,
                EnableSnap = true,
                SnapX = 10,
                SnapY = 10,
            },
        };

        Dictionary<int, int> lExecOutputs = pGraph.Nodes.ToDictionary(pNode => pNode.Id, pNode => ExecOutputCount(pGraph, pNode));
        Dictionary<int, (double X, double Y)> lLayout = BlueprintLayout.Arrange(pGraph, pId => Height(pGraph.Nodes[pId].Definition, lExecOutputs[pId]));

        Dictionary<int, IPin> lExecIn = new Dictionary<int, IPin>();
        Dictionary<(int Node, int Pin), IPin> lExecOut = new Dictionary<(int Node, int Pin), IPin>();
        Dictionary<(int Node, int Pin), IPin> lDataIn = new Dictionary<(int Node, int Pin), IPin>();
        Dictionary<(int Node, int Pin), IPin> lDataOut = new Dictionary<(int Node, int Pin), IPin>();
        double lWidth = 0;
        double lHeight = 0;
        foreach (NodeInstance lInstance in pGraph.Nodes)
        {
            IBlueprintNode lDefinition = lInstance.Definition;
            int lExecIns = lDefinition is IActionNode ? 1 : 0;
            int lExecOuts = lExecOutputs[lInstance.Id];
            (double lX, double lY) = lLayout[lInstance.Id];
            NodeViewModel lNode = new NodeViewModel
            {
                Name = lDefinition.TypeId,
                Parent = Drawing,
                X = lX + Margin,
                Y = lY + Margin,
                Width = NodeWidth,
                Height = Height(lDefinition, lExecOuts),
                Pins = new ObservableCollection<IPin>(),
            };
            lWidth = Math.Max(lWidth, lNode.X + lNode.Width + Margin);
            lHeight = Math.Max(lHeight, lNode.Y + lNode.Height + Margin);
            NodeContent lContent = new NodeContent(ShortName(lDefinition), HeaderColor(lDefinition), Rows(lDefinition, lExecOuts));

            if (lExecIns > 0)
            {
                lExecIn[lInstance.Id] = AddPin(lNode, 0, RowY(0), PinAlignment.Left, "exec", PinDirection.Input);
            }

            for (int lPin = 0; lPin < lExecOuts; lPin++)
            {
                lExecOut[(lInstance.Id, lPin)] = AddPin(lNode, NodeWidth, RowY(lPin), PinAlignment.Right, "exec", PinDirection.Output);
                lContent.Rows[lPin].Right = ExecLabel(lDefinition, lPin, lExecOuts);
            }

            for (int lPin = 0; lPin < lDefinition.Inputs.Length; lPin++)
            {
                PinDef lDef = lDefinition.Inputs[lPin];
                lDataIn[(lInstance.Id, lPin)] = AddPin(lNode, 0, RowY(lExecIns + lPin), PinAlignment.Left, lDef.Type.ToString(), PinDirection.Input);
                lContent.Rows[lExecIns + lPin].Left = pGraph.TryGetSource(lInstance.Id, lPin, out _)
                    ? lDef.Name
                    : $"{lDef.Name} = {Format(lInstance.Constants[lPin])}";
            }

            for (int lPin = 0; lPin < lDefinition.Outputs.Length; lPin++)
            {
                PinDef lDef = lDefinition.Outputs[lPin];
                lDataOut[(lInstance.Id, lPin)] = AddPin(lNode, NodeWidth, RowY(lExecOuts + lPin), PinAlignment.Right, lDef.Type.ToString(), PinDirection.Output);
                lContent.Rows[lExecOuts + lPin].Right = lDef.Name;
            }

            lNode.Content = lContent;
            mContents[lInstance.Id] = lContent;
            lNodes.Add(lNode);
        }

        Drawing.Width = lWidth;
        Drawing.Height = lHeight;

        foreach ((int lFrom, int lFromPin, int lTo) in pGraph.ExecLinks)
        {
            lConnectors.Add(new ConnectorViewModel { Parent = Drawing, Start = lExecOut[(lFrom, lFromPin)], End = lExecIn[lTo] });
        }

        foreach ((int lFrom, int lFromPin, int lTo, int lToPin) in pGraph.DataLinks)
        {
            lConnectors.Add(new ConnectorViewModel
            {
                Parent = Drawing,
                Start = lDataOut[(lFrom, lFromPin)],
                End = lDataIn[(lTo, lToPin)],
            });
        }
    }

    /// <summary>
    /// Gets the entity the graph is attached to.
    /// </summary>
    public Entity Owner { get; }

    /// <summary>
    /// Gets the id of the graph in the runtime.
    /// </summary>
    public int GraphId { get; }

    /// <summary>
    /// Gets the label shown in the list of graphs.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the drawing shown by the node editor.
    /// </summary>
    public DrawingNodeViewModel Drawing { get; }

    /// <inheritdoc/>
    public override string ToString() => Label;

    /// <summary>
    /// Signals that a node ran, and the pure nodes feeding it with it.
    /// </summary>
    /// <param name="pNode">The id of the node.</param>
    public void Flash(int pNode)
    {
        mFlashes[pNode] = DateTime.Now;
        for (int lPin = 0; lPin < mGraph.Nodes[pNode].Definition.Inputs.Length; lPin++)
        {
            if (mGraph.TryGetSource(pNode, lPin, out (int Node, int Pin) lSource) && mGraph.Nodes[lSource.Node].Definition is IPureNode)
            {
                Flash(lSource.Node);
            }
        }
    }

    /// <summary>
    /// Copies the fibers waiting on each node, read from the store, and fades the halos.
    /// </summary>
    /// <param name="pStore">The store of the simulation.</param>
    /// <param name="pSimTime">The simulation time.</param>
    public void Update(EntityStore pStore, TimeSpan pSimTime)
    {
        ArgumentNullException.ThrowIfNull(pStore);
        Dictionary<int, List<string>> lWaiting = new Dictionary<int, List<string>>();
        foreach (Entity lFiber in pStore.Query<FiberState>().Entities)
        {
            FiberState lState = lFiber.GetComponent<FiberState>();
            if (lState.NodeId < 0 || lState.Owner.Id != Owner.Id || lState.Key.GraphId != GraphId)
            {
                continue;
            }

            // A delay shows what is left; a task, which polls at each step, shows no time.
            TimeSpan lLeft = lFiber.TryGetComponent(out FiberWait lWait) ? lWait.WakeTime - pSimTime : TimeSpan.Zero;
            string lInfo = lLeft > sShownWait && lLeft < TimeSpan.FromDays(1)
                ? string.Create(CultureInfo.InvariantCulture, $"fibre #{lFiber.Id} · {lLeft.TotalSeconds:F0} s")
                : $"fibre #{lFiber.Id}";
            if (!lWaiting.TryGetValue(lState.NodeId, out List<string>? lList))
            {
                lWaiting[lState.NodeId] = lList = new List<string>();
            }

            lList.Add(lInfo);
        }

        DateTime lNow = DateTime.Now;
        foreach ((int lId, NodeContent lContent) in mContents)
        {
            lContent.Badge = lWaiting.TryGetValue(lId, out List<string>? lFibers) ? "⏸ " + string.Join("  |  ", lFibers) : null;
            lContent.Flash = mFlashes.TryGetValue(lId, out DateTime lAt) ? Math.Max(0, 1 - ((lNow - lAt) / sFlashDuration)) : 0;
        }
    }

    private static IPin AddPin(NodeViewModel pNode, double pX, double pY, PinAlignment pAlignment, string pName, PinDirection pDirection)
    {
        IPin lPin = pNode.AddPin(pX, pY, PinSize, PinSize, pAlignment, pName);
        if (lPin is PinViewModel lViewModel)
        {
            lViewModel.Direction = pDirection;
        }

        return lPin;
    }

    // Exec outputs: one for an event or an action, more when the graph links further ones (task outcome, branch).
    private static int ExecOutputCount(BlueprintGraph pGraph, NodeInstance pNode)
    {
        if (pNode.Definition is IPureNode)
        {
            return 0;
        }

        int lLinked = pGraph.ExecLinks.Where(pLink => pLink.From == pNode.Id).Select(pLink => pLink.FromPin + 1).DefaultIfEmpty(1).Max();
        int lDeclared = pNode.Definition is TaskNode or Branch ? 2 : 1;
        return Math.Max(lLinked, lDeclared);
    }

    private static int Rows(IBlueprintNode pDefinition, int pExecOutputs) => Math.Max(1, Math.Max(
        (pDefinition is IActionNode ? 1 : 0) + pDefinition.Inputs.Length,
        pExecOutputs + pDefinition.Outputs.Length));

    private static double Height(IBlueprintNode pDefinition, int pExecOutputs) => Header + (Rows(pDefinition, pExecOutputs) * RowHeight) + 8;

    private static double RowY(int pRow) => Header + 4 + (pRow * RowHeight) + (RowHeight / 2);

    private static string ExecLabel(IBlueprintNode pDefinition, int pPin, int pCount) => pDefinition switch
    {
        TaskNode => pPin == TaskNode.SuccessPin ? "Succès" : "Échec",
        Branch => pPin == Branch.TruePin ? "Vrai" : "Faux",
        _ => pCount > 1 ? $"Sortie {pPin}" : string.Empty,
    };

    private static Color HeaderColor(IBlueprintNode pDefinition) => Color.Parse(pDefinition switch
    {
        IEventNode => "#A83A3A",
        Delay or TaskNode => "#2B8A8A",
        IActionNode => "#2F64A8",
        _ => "#3E8A43",
    });

    private static string ShortName(IBlueprintNode pDefinition)
    {
        int lDot = pDefinition.TypeId.IndexOf('.', StringComparison.Ordinal);
        return lDot >= 0 ? pDefinition.TypeId[(lDot + 1)..] : pDefinition.TypeId;
    }

    private static string Format(object? pValue) => pValue switch
    {
        null => "—",
        Entity lEntity => lEntity.TryGetComponent(out Force lForce) ? lForce.Callsign : $"#{lEntity.Id}",
        bool lBool => lBool ? "vrai" : "faux",
        double lNumber => lNumber.ToString("0.##", CultureInfo.InvariantCulture),
        float lNumber => lNumber.ToString("0.##", CultureInfo.InvariantCulture),
        TimeSpan lTime => lTime.ToString("c", CultureInfo.InvariantCulture),
        GeoPosition lPosition => string.Create(CultureInfo.InvariantCulture, $"{lPosition.Latitude:0.##}° {lPosition.Longitude:0.##}° {lPosition.Altitude:0} m"),
        IReadOnlyList<CourseWaypoint> lCourse => $"{lCourse.Count} points",
        _ => Convert.ToString(pValue, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
