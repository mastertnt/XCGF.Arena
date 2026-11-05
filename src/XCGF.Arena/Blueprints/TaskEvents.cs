using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;

namespace XCGF.Arena.Blueprints;

/// <summary>
/// Subscription of a blueprint to the end of the tasks of its entity, carried by an entity of its own. The
/// <see cref="Nodes.OnTaskEnded"/> event creates it; <see cref="TaskEndDetectionSystem"/> reads it.
/// </summary>
/// <param name="Owner">Entity whose blueprint listens, and whose tasks are watched.</param>
/// <param name="Key">The event to fire.</param>
/// <param name="Task">Name of the task watched, or null for any task.</param>
/// <param name="Seen">The ended tasks already reported.</param>
public record struct TaskEndSubscription(Entity Owner, EventKey Key, string? Task, HashSet<TaskInstance> Seen) : IComponent;

/// <summary>
/// Fires the task end events: for each subscription, reports once each ended task of the owner that it watches.
/// </summary>
public sealed class TaskEndDetectionSystem : QuerySystem<TaskEndSubscription>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskEndDetectionSystem"/> class.
    /// </summary>
    /// <param name="pRuntime">The runtime to fire the events in.</param>
    public TaskEndDetectionSystem(BlueprintRuntime pRuntime)
    {
        ArgumentNullException.ThrowIfNull(pRuntime);
        Runtime = pRuntime;
    }

    /// <summary>
    /// Gets the runtime the events are fired in.
    /// </summary>
    public BlueprintRuntime Runtime { get; }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TaskEndSubscription pSubscription, Entity pEntity) =>
        {
            if (pSubscription.Owner.IsNull || !pSubscription.Owner.TryGetComponent(out AssignedTasks lTasks))
            {
                return;
            }

            foreach (TaskInstance lTask in lTasks.Items)
            {
                if (lTask.State == TaskState.Ended
                    && (pSubscription.Task is null || pSubscription.Task == lTask.Definition.Name)
                    && pSubscription.Seen.Add(lTask))
                {
                    Runtime.Trigger(pSubscription.Owner, pSubscription.Key, new object?[] { lTask.Definition.Name, lTask.Outcome });
                }
            }
        });
    }
}
