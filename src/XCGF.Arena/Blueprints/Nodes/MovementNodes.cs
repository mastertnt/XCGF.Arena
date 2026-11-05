using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Blueprints.Nodes;

/// <summary>
/// Latent action that assigns an XCGF.Data task to the entity, then suspends the fiber until the task ends.
/// </summary>
/// <remarks>
/// The fiber follows the exec output <see cref="SuccessPin"/> when the task ends with the Success outcome, and
/// <see cref="FailurePin"/> otherwise (e.g. Aborted on OrderCancelled). The data output 0 gives the outcome. Ending the
/// fiber early (Restart policy) leaves the task running.
/// </remarks>
public abstract class TaskNode : BlueprintNode, IActionNode
{
    /// <summary>
    /// Exec output followed when the task ends with the Success outcome.
    /// </summary>
    public const int SuccessPin = 0;

    /// <summary>
    /// Exec output followed when the task ends with another outcome.
    /// </summary>
    public const int FailurePin = 1;

    /// <summary>
    /// Outcome of a task that succeeded.
    /// </summary>
    public const string SuccessOutcome = "Success";

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskNode"/> class.
    /// </summary>
    /// <param name="pTasks">The task system.</param>
    /// <param name="pLibrary">The library declaring the task.</param>
    /// <param name="pTask">Name of the task.</param>
    /// <param name="pInputs">The data inputs, mapped to task parameters by <see cref="BuildValues"/>.</param>
    protected TaskNode(TaskSystem pTasks, ArchetypeLibrary pLibrary, string pTask, PinDef[] pInputs)
        : base("Task." + pTask, pInputs, new[] { new PinDef("Outcome", PinType.Text) })
    {
        ArgumentNullException.ThrowIfNull(pTasks);
        ArgumentNullException.ThrowIfNull(pLibrary);
        Tasks = pTasks;
        Library = pLibrary;
        Task = pTask;
    }

    /// <summary>
    /// Gets the name of the task.
    /// </summary>
    public string Task { get; }

    /// <summary>
    /// Gets the task system.
    /// </summary>
    protected TaskSystem Tasks { get; }

    /// <summary>
    /// Gets the library declaring the task.
    /// </summary>
    protected ArchetypeLibrary Library { get; }

    /// <inheritdoc/>
    public ExecResult Execute(IExecContext pContext, NodeArgs pArgs)
    {
        TaskInstance lInstance = Tasks.Assign(pContext.Self, Task, BuildValues(pArgs));
        pArgs.SetState(lInstance);
        pContext.Log($"task {Task} assigned");
        return ExecResult.WaitUntil(pContext.SimTime);
    }

    /// <inheritdoc/>
    public ExecResult Resume(IExecContext pContext, NodeArgs pArgs)
    {
        TaskInstance lInstance = pArgs.GetState<TaskInstance>() ?? throw new InvalidOperationException($"{TypeId} resumed without its task.");
        if (lInstance.State != TaskState.Ended)
        {
            // Checked again at the next step.
            return ExecResult.WaitUntil(pContext.SimTime);
        }

        pArgs.SetState(null);
        pArgs.Out(0, lInstance.Outcome);
        pContext.Log($"task {Task} ended: {lInstance.Outcome}");
        return ExecResult.Next(lInstance.Outcome == SuccessOutcome ? SuccessPin : FailurePin);
    }

    /// <summary>
    /// Builds the scenario values of the task parameters from the data inputs. An input without value leaves its
    /// parameter to its default.
    /// </summary>
    /// <param name="pArgs">Access to the pins.</param>
    /// <returns>The values, by parameter name.</returns>
    protected abstract Dictionary<string, DataValue> BuildValues(NodeArgs pArgs);

    /// <summary>
    /// Parses a value written as in a library, according to the data type of a task parameter, e.g. a Throttle name.
    /// </summary>
    /// <param name="pParameter">Name of the parameter.</param>
    /// <param name="pText">The text.</param>
    /// <returns>The value.</returns>
    protected DataValue ParseParameter(string pParameter, string pText)
    {
        GroupedTaskParameter lParameter = Library.GetTask(Task).FindParameter(pParameter)
            ?? throw new XcgfDataException($"Task '{Task}' has no parameter '{pParameter}'.");
        return Library.ParseValue(lParameter.Parameter.DataType, pText);
    }
}

/// <summary>
/// MoveTo task: moves to Destination, at Speed when set, or else at the speed of Throttle (Cruise by default).
/// </summary>
public sealed class MoveTo : TaskNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MoveTo"/> class.
    /// </summary>
    /// <param name="pTasks">The task system.</param>
    /// <param name="pLibrary">The library declaring the task.</param>
    public MoveTo(TaskSystem pTasks, ArchetypeLibrary pLibrary)
        : base(pTasks, pLibrary, MoveToBehavior.TaskName, new[] { new PinDef("Destination", PinType.Position), new PinDef("Speed", PinType.Number), new PinDef("Throttle", PinType.Text) })
    {
    }

    /// <inheritdoc/>
    protected override Dictionary<string, DataValue> BuildValues(NodeArgs pArgs)
    {
        Dictionary<string, DataValue> lValues = new Dictionary<string, DataValue>(StringComparer.Ordinal)
        {
            ["Destination"] = pArgs.In<GeoPosition>(0).ToValue(),
        };
        if (pArgs.HasValue(1))
        {
            lValues["Speed"] = new NumberValue(pArgs.In<double>(1));
        }

        if (pArgs.HasValue(2))
        {
            lValues["Throttle"] = ParseParameter("Throttle", pArgs.In<string>(2));
        }

        return lValues;
    }
}

/// <summary>
/// FollowCourse task: goes through Waypoints in order, at the speed of each waypoint, or else of Throttle.
/// </summary>
public sealed class FollowCourse : TaskNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FollowCourse"/> class.
    /// </summary>
    /// <param name="pTasks">The task system.</param>
    /// <param name="pLibrary">The library declaring the task.</param>
    public FollowCourse(TaskSystem pTasks, ArchetypeLibrary pLibrary)
        : base(pTasks, pLibrary, FollowCourseBehavior.TaskName, new[] { new PinDef("Waypoints", PinType.Course), new PinDef("Throttle", PinType.Text), new PinDef("Loop", PinType.Bool) })
    {
    }

    /// <inheritdoc/>
    protected override Dictionary<string, DataValue> BuildValues(NodeArgs pArgs)
    {
        List<DataValue> lWaypoints = new List<DataValue>();
        foreach (CourseWaypoint lWaypoint in pArgs.In<IReadOnlyList<CourseWaypoint>>(0))
        {
            lWaypoints.Add(lWaypoint.Speed is { } lSpeed
                ? RecordValue.Of(("Position", lWaypoint.Position.ToValue()), ("Speed", new NumberValue(lSpeed)))
                : RecordValue.Of(("Position", lWaypoint.Position.ToValue())));
        }

        Dictionary<string, DataValue> lValues = new Dictionary<string, DataValue>(StringComparer.Ordinal)
        {
            ["Waypoints"] = new ArrayValue(lWaypoints),
        };
        if (pArgs.HasValue(1))
        {
            lValues["Throttle"] = ParseParameter("Throttle", pArgs.In<string>(1));
        }

        if (pArgs.HasValue(2))
        {
            lValues["Loop"] = ParseParameter("Loop", pArgs.In<bool>(2) ? "HLAtrue" : "HLAfalse");
        }

        return lValues;
    }
}

/// <summary>
/// HoldPosition task: stays at the current position for Duration seconds, or until cancelled when unset.
/// </summary>
public sealed class HoldPosition : TaskNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HoldPosition"/> class.
    /// </summary>
    /// <param name="pTasks">The task system.</param>
    /// <param name="pLibrary">The library declaring the task.</param>
    public HoldPosition(TaskSystem pTasks, ArchetypeLibrary pLibrary)
        : base(pTasks, pLibrary, HoldPositionBehavior.TaskName, new[] { new PinDef("Duration", PinType.Number) })
    {
    }

    /// <inheritdoc/>
    protected override Dictionary<string, DataValue> BuildValues(NodeArgs pArgs)
    {
        Dictionary<string, DataValue> lValues = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        if (pArgs.HasValue(0))
        {
            lValues["Duration"] = new NumberValue(pArgs.In<double>(0));
        }

        return lValues;
    }
}

/// <summary>
/// Fires when a task of the entity ends. Input: the task name, or null for any task. Outputs: the task name, its outcome.
/// </summary>
public sealed class OnTaskEnded : BlueprintNode, IEventNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OnTaskEnded"/> class.
    /// </summary>
    /// <param name="pPolicy">What happens on an end while the fiber of the previous one runs.</param>
    public OnTaskEnded(RetriggerPolicy pPolicy = RetriggerPolicy.Parallel)
        : base("Event.OnTaskEnded", new[] { new PinDef("Task", PinType.Text) }, new[] { new PinDef("Task", PinType.Text), new PinDef("Outcome", PinType.Text) })
    {
        Policy = pPolicy;
    }

    /// <inheritdoc/>
    public RetriggerPolicy Policy { get; }

    /// <inheritdoc/>
    public void Subscribe(IExecContext pContext, NodeArgs pParameters, EventKey pKey)
    {
        string? lTask = pParameters.HasValue(0) ? pParameters.In<string>(0) : null;
        pContext.Store.CreateEntity(new TaskEndSubscription(pContext.Self, pKey, lTask, new HashSet<TaskInstance>()));
    }

    /// <inheritdoc/>
    public void WritePayload(object?[] pPayload, NodeArgs pArgs)
    {
        pArgs.Out(0, pPayload[0]);
        pArgs.Out(1, pPayload[1]);
    }
}

/// <summary>
/// Pure: a geographic position from its coordinates.
/// </summary>
public sealed class MakePosition : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MakePosition"/> class.
    /// </summary>
    public MakePosition()
        : base("Geo.MakePosition", new[] { new PinDef("Latitude", PinType.Number), new PinDef("Longitude", PinType.Number), new PinDef("Altitude", PinType.Number) }, new[] { new PinDef("Position", PinType.Position) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs)
    {
        pArgs.Out(0, new GeoPosition(pArgs.In<double>(0), pArgs.In<double>(1), pArgs.HasValue(2) ? pArgs.In<double>(2) : 0));
    }
}

/// <summary>
/// Pure: the position of an entity.
/// </summary>
public sealed class GetPosition : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetPosition"/> class.
    /// </summary>
    public GetPosition()
        : base("Kinematics.GetPosition", new[] { new PinDef("Entity", PinType.Entity) }, new[] { new PinDef("Position", PinType.Position) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, pArgs.In<Entity>(0).GetComponent<Kinematic>().Position);
}

/// <summary>
/// Pure: the position reached from a position along a geodesic, at the same altitude.
/// </summary>
public sealed class OffsetPosition : BlueprintNode, IPureNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetPosition"/> class.
    /// </summary>
    public OffsetPosition()
        : base("Geo.Offset", new[] { new PinDef("From", PinType.Position), new PinDef("Bearing", PinType.Number), new PinDef("Distance", PinType.Number) }, new[] { new PinDef("Position", PinType.Position) })
    {
    }

    /// <inheritdoc/>
    public void Evaluate(IReadContext pContext, NodeArgs pArgs) => pArgs.Out(0, GeoMath.Offset(pArgs.In<GeoPosition>(0), pArgs.In<double>(1), pArgs.In<double>(2)));
}
