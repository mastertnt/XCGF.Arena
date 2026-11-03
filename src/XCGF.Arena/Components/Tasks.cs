using Friflo.Engine.ECS;
using XCGF.Arena.Data;

namespace XCGF.Arena.Components;

/// <summary>
/// State of an assigned task.
/// </summary>
public enum TaskState
{
    /// <summary>Waiting for its activation trigger.</summary>
    Pending,

    /// <summary>Activated, waiting for an exit trigger.</summary>
    Running,

    /// <summary>Ended by an exit trigger.</summary>
    Ended,
}

/// <summary>
/// Task assigned to a carrier.
/// </summary>
public sealed class TaskInstance
{
    private readonly HashSet<string> mEvents = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskInstance"/> class.
    /// </summary>
    /// <param name="pDefinition">The task.</param>
    /// <param name="pParameters">Values of the parameters, by name.</param>
    /// <param name="pAssignedAt">Simulation time of the assignment.</param>
    public TaskInstance(TaskDefinition pDefinition, Dictionary<string, DataValue?> pParameters, TimeSpan pAssignedAt)
    {
        Definition = pDefinition;
        Parameters = pParameters;
        AssignedAt = pAssignedAt;
    }

    /// <summary>
    /// Gets the task.
    /// </summary>
    public TaskDefinition Definition { get; }

    /// <summary>
    /// Gets the values of the parameters, by name. Out and InOut parameters are updated by the task.
    /// </summary>
    public Dictionary<string, DataValue?> Parameters { get; }

    /// <summary>
    /// Gets the simulation time of the assignment.
    /// </summary>
    public TimeSpan AssignedAt { get; }

    /// <summary>
    /// Gets the state.
    /// </summary>
    public TaskState State { get; internal set; }

    /// <summary>
    /// Gets the simulation time the task started, once running.
    /// </summary>
    public TimeSpan? StartedAt { get; internal set; }

    /// <summary>
    /// Gets the simulation time the task ended, once ended.
    /// </summary>
    public TimeSpan? EndedAt { get; internal set; }

    /// <summary>
    /// Gets the outcome, once ended.
    /// </summary>
    public string? Outcome { get; internal set; }

    /// <summary>
    /// Gets the events raised for the task since the last step.
    /// </summary>
    public IReadOnlySet<string> Events => mEvents;

    internal void AddEvent(string pEvent) => mEvents.Add(pEvent);

    internal void ClearEvents() => mEvents.Clear();
}

/// <summary>
/// Tasks assigned to a carrier, ended ones included, in assignment order.
/// </summary>
/// <param name="Items">The tasks.</param>
public record struct AssignedTasks(List<TaskInstance> Items) : IComponent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignedTasks"/> struct, with no task.
    /// </summary>
    public AssignedTasks()
        : this(new List<TaskInstance>())
    {
    }
}
