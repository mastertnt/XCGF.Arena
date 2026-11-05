using System.Globalization;
using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Blueprints.Nodes;

/// <summary>
/// Base of the standard nodes: the type identifier and the data pins.
/// </summary>
public abstract class BlueprintNode : IBlueprintNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintNode"/> class.
    /// </summary>
    /// <param name="pTypeId">The identifier of the node kind.</param>
    /// <param name="pInputs">The data inputs.</param>
    /// <param name="pOutputs">The data outputs.</param>
    protected BlueprintNode(string pTypeId, PinDef[] pInputs, PinDef[] pOutputs)
    {
        TypeId = pTypeId;
        Inputs = pInputs;
        Outputs = pOutputs;
    }

    /// <inheritdoc/>
    public string TypeId { get; }

    /// <inheritdoc/>
    public PinDef[] Inputs { get; }

    /// <inheritdoc/>
    public PinDef[] Outputs { get; }
}

// ===========================================================================
// Events
// ===========================================================================

/// <summary>
/// Fires once, when the graph is attached.
/// </summary>
public sealed class OnStart : BlueprintNode, IEventNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OnStart"/> class.
    /// </summary>
    public OnStart()
        : base("Event.OnStart", Array.Empty<PinDef>(), Array.Empty<PinDef>())
    {
    }

    /// <inheritdoc/>
    public RetriggerPolicy Policy => RetriggerPolicy.Once;

    /// <inheritdoc/>
    public void Subscribe(IExecContext pContext, NodeArgs pParameters, EventKey pKey) => pContext.Runtime.Trigger(pContext.Self, pKey, Array.Empty<object?>());

    /// <inheritdoc/>
    public void WritePayload(object?[] pPayload, NodeArgs pArgs)
    {
    }
}

/// <summary>
/// Fires when the entity enters a <see cref="CircularZone"/>. Outputs: the zone, the time of entry.
/// </summary>
public sealed class OnZoneEnter : BlueprintNode, IEventNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OnZoneEnter"/> class.
    /// </summary>
    /// <param name="pPolicy">What happens on an entry while the fiber of the previous one runs.</param>
    public OnZoneEnter(RetriggerPolicy pPolicy = RetriggerPolicy.Once)
        : base("Event.OnZoneEnter", new[] { new PinDef("Zone", PinType.Entity) }, new[] { new PinDef("Zone", PinType.Entity), new PinDef("EntryTime", PinType.Time) })
    {
        Policy = pPolicy;
    }

    /// <inheritdoc/>
    public RetriggerPolicy Policy { get; }

    /// <inheritdoc/>
    public void Subscribe(IExecContext pContext, NodeArgs pParameters, EventKey pKey)
    {
        pContext.Store.CreateEntity(new ZoneSubscription(pContext.Self, pKey, pParameters.In<Entity>(0), true, false));
    }

    /// <inheritdoc/>
    public void WritePayload(object?[] pPayload, NodeArgs pArgs)
    {
        pArgs.Out(0, pPayload[0]);
        pArgs.Out(1, pPayload[1]);
    }
}

// ===========================================================================
// Actions
// ===========================================================================

/// <summary>
/// Sets the speed of the entity, in metres per second. The kinematic system keeps it within the limits.
/// </summary>
public sealed class SetSpeed : BlueprintNode, IActionNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetSpeed"/> class.
    /// </summary>
    public SetSpeed()
        : base("Kinematics.SetSpeed", new[] { new PinDef("Speed", PinType.Number) }, Array.Empty<PinDef>())
    {
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        double lSpeed = pArgs.In<double>(0);
        pContext.Self.GetComponent<Kinematic>().Speed = lSpeed;
        pContext.Log(string.Create(CultureInfo.InvariantCulture, $"speed {lSpeed} m/s"));
        return ExecResult.Next();
    }
}

/// <summary>
/// Sets the heading of the entity, in degrees from true north.
/// </summary>
public sealed class SetHeading : BlueprintNode, IActionNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetHeading"/> class.
    /// </summary>
    public SetHeading()
        : base("Kinematics.SetHeading", new[] { new PinDef("Heading", PinType.Number) }, Array.Empty<PinDef>())
    {
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        ref Kinematic lKinematic = ref pContext.Self.GetComponent<Kinematic>();
        double lHeading = GeoMath.NormalizeHeading(pArgs.In<double>(0));
        lKinematic.Orientation = lKinematic.Orientation with { Heading = lHeading };
        pContext.Log(string.Create(CultureInfo.InvariantCulture, $"heading {lHeading:F1} deg"));
        return ExecResult.Next();
    }
}

/// <summary>
/// Sets an initialization parameter of an equipment of the entity, e.g. the Mode of its Radar. The value is written as in
/// a library and parsed according to the data type of the parameter.
/// </summary>
public sealed class SetEquipmentParameter : BlueprintNode, IActionNode
{
    private readonly ArchetypeLibrary mLibrary;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetEquipmentParameter"/> class.
    /// </summary>
    /// <param name="pLibrary">The library declaring the data types.</param>
    public SetEquipmentParameter(ArchetypeLibrary pLibrary)
        : base("Equipment.SetParameter", new[] { new PinDef("Equipment", PinType.Text), new PinDef("Parameter", PinType.Text), new PinDef("Value", PinType.Text) }, Array.Empty<PinDef>())
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        mLibrary = pLibrary;
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        string lName = pArgs.In<string>(0);
        string lParameter = pArgs.In<string>(1);
        string lText = pArgs.In<string>(2);
        Entity lEquipment = pContext.Self.FindEquipment(lName);
        if (lEquipment.IsNull || !lEquipment.TryGetComponent(out EquipmentData lData))
        {
            throw new XcgfDataException($"Entity #{pContext.Self.Id} has no equipment '{lName}'.");
        }

        if (!lData.Initialization.Items.TryGetValue(lParameter, out DataItem? lItem))
        {
            throw new XcgfDataException($"Equipment '{lName}' has no initialization parameter '{lParameter}'.");
        }

        lData.Initialization.Set(lParameter, mLibrary.ParseValue(lItem.DataType, lText));
        pContext.Log($"{lName}.{lParameter} = {lText}");
        return ExecResult.Next();
    }
}

/// <summary>
/// Assigns an XCGF.Data task to the entity, with its default parameters.
/// </summary>
public sealed class AssignTask : BlueprintNode, IActionNode
{
    private readonly TaskSystem mTasks;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignTask"/> class.
    /// </summary>
    /// <param name="pTasks">The task system.</param>
    public AssignTask(TaskSystem pTasks)
        : base("Task.Assign", new[] { new PinDef("Task", PinType.Text) }, Array.Empty<PinDef>())
    {
        ArgumentNullException.ThrowIfNull(pTasks);
        mTasks = pTasks;
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        string lTask = pArgs.In<string>(0);
        mTasks.Assign(pContext.Self, lTask);
        pContext.Log($"task {lTask} assigned");
        return ExecResult.Next();
    }
}

/// <summary>
/// Raises an event for the tasks of the entity, seen by their OnEvent triggers.
/// </summary>
public sealed class RaiseTaskEvent : BlueprintNode, IActionNode
{
    private readonly TaskSystem mTasks;

    /// <summary>
    /// Initializes a new instance of the <see cref="RaiseTaskEvent"/> class.
    /// </summary>
    /// <param name="pTasks">The task system.</param>
    public RaiseTaskEvent(TaskSystem pTasks)
        : base("Task.RaiseEvent", new[] { new PinDef("Event", PinType.Text) }, Array.Empty<PinDef>())
    {
        ArgumentNullException.ThrowIfNull(pTasks);
        mTasks = pTasks;
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        string lEvent = pArgs.In<string>(0);
        mTasks.RaiseEvent(pContext.Self, lEvent);
        pContext.Log($"task event {lEvent}");
        return ExecResult.Next();
    }
}

/// <summary>
/// Writes a message to the runtime log.
/// </summary>
public sealed class LogMessage : BlueprintNode, IActionNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogMessage"/> class.
    /// </summary>
    public LogMessage()
        : base("Debug.Log", new[] { new PinDef("Message", PinType.Text) }, Array.Empty<PinDef>())
    {
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        pContext.Log(pArgs.In<string>(0));
        return ExecResult.Next();
    }
}

/// <summary>
/// Latent: suspends the fiber for a number of seconds of simulation time, then follows its exec output 0.
/// </summary>
public sealed class Delay : BlueprintNode, IActionNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Delay"/> class.
    /// </summary>
    public Delay()
        : base("Flow.Delay", new[] { new PinDef("Seconds", PinType.Number) }, Array.Empty<PinDef>())
    {
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        double lSeconds = pArgs.In<double>(0);
        pContext.Log(string.Create(CultureInfo.InvariantCulture, $"wait {lSeconds} s"));
        return ExecResult.WaitUntil(pContext.SimTime + TimeSpan.FromSeconds(lSeconds));
    }
}

/// <summary>
/// Flow control: follows the exec output 0 when the condition is true, 1 otherwise.
/// </summary>
public sealed class Branch : BlueprintNode, IActionNode
{
    /// <summary>
    /// Exec output followed when the condition is true.
    /// </summary>
    public const int TruePin = 0;

    /// <summary>
    /// Exec output followed when the condition is false.
    /// </summary>
    public const int FalsePin = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Branch"/> class.
    /// </summary>
    public Branch()
        : base("Flow.Branch", new[] { new PinDef("Condition", PinType.Bool) }, Array.Empty<PinDef>())
    {
    }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs) => ExecResult.Next(pArgs.In<bool>(0) ? TruePin : FalsePin);
}

// ===========================================================================
// Pures
// ===========================================================================

/// <summary>
/// The entity the graph is attached to.
/// </summary>
public sealed class Self : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Self"/> class.
    /// </summary>
    public Self()
        : base("Entity.Self", Array.Empty<PinDef>(), new[] { new PinDef("Self", PinType.Entity) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pContext.Self);
}

/// <summary>
/// The heading of an entity, in degrees from true north.
/// </summary>
public sealed class GetHeading : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetHeading"/> class.
    /// </summary>
    public GetHeading()
        : base("Kinematics.GetHeading", new[] { new PinDef("Entity", PinType.Entity) }, new[] { new PinDef("Heading", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<Entity>(0).GetComponent<Kinematic>().Orientation.Heading);
}

/// <summary>
/// The speed of an entity, in metres per second.
/// </summary>
public sealed class GetSpeed : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetSpeed"/> class.
    /// </summary>
    public GetSpeed()
        : base("Kinematics.GetSpeed", new[] { new PinDef("Entity", PinType.Entity) }, new[] { new PinDef("Speed", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<Entity>(0).GetComponent<Kinematic>().Speed);
}

/// <summary>
/// The altitude of an entity, in metres.
/// </summary>
public sealed class GetAltitude : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetAltitude"/> class.
    /// </summary>
    public GetAltitude()
        : base("Kinematics.GetAltitude", new[] { new PinDef("Entity", PinType.Entity) }, new[] { new PinDef("Altitude", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<Entity>(0).GetComponent<Kinematic>().Position.Altitude);
}

/// <summary>
/// Adds an angle to a heading, the result in [0, 360) degrees.
/// </summary>
public sealed class AddHeading : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddHeading"/> class.
    /// </summary>
    public AddHeading()
        : base("Math.AddHeading", new[] { new PinDef("Heading", PinType.Number), new PinDef("Delta", PinType.Number) }, new[] { new PinDef("Result", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, GeoMath.NormalizeHeading(pArgs.In<double>(0) + pArgs.In<double>(1)));
}

/// <summary>
/// A + B.
/// </summary>
public sealed class Add : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Add"/> class.
    /// </summary>
    public Add()
        : base("Math.Add", new[] { new PinDef("A", PinType.Number), new PinDef("B", PinType.Number) }, new[] { new PinDef("Result", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<double>(0) + pArgs.In<double>(1));
}

/// <summary>
/// A - B.
/// </summary>
public sealed class Subtract : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Subtract"/> class.
    /// </summary>
    public Subtract()
        : base("Math.Subtract", new[] { new PinDef("A", PinType.Number), new PinDef("B", PinType.Number) }, new[] { new PinDef("Result", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<double>(0) - pArgs.In<double>(1));
}

/// <summary>
/// A &gt; B.
/// </summary>
public sealed class GreaterThan : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GreaterThan"/> class.
    /// </summary>
    public GreaterThan()
        : base("Math.GreaterThan", new[] { new PinDef("A", PinType.Number), new PinDef("B", PinType.Number) }, new[] { new PinDef("Result", PinType.Bool) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<double>(0) > pArgs.In<double>(1));
}
