using XCGF.Arena.Components;
using XCGF.Arena.Ecs;

namespace XCGF.Arena.Systems;

/// <summary>
/// Context given to task behaviours and trigger handlers.
/// </summary>
/// <param name="World">The world.</param>
/// <param name="Carrier">Entity or unit the task is assigned to.</param>
/// <param name="Task">The task.</param>
/// <param name="Time">Time of the step.</param>
public sealed record TaskContext(World World, Entity Carrier, TaskInstance Task, SimulationTime Time);

/// <summary>
/// What a task does while it runs. The task system only handles the life cycle; a behaviour acts on the world.
/// </summary>
public interface ITaskBehavior
{
    /// <summary>
    /// Called once when the activation trigger fires.
    /// </summary>
    /// <param name="pContext">The context.</param>
    void OnStart(TaskContext pContext);

    /// <summary>
    /// Called at each step while the task runs, before its exit triggers are evaluated.
    /// </summary>
    /// <param name="pContext">The context.</param>
    void OnUpdate(TaskContext pContext);

    /// <summary>
    /// Called once when an exit trigger fires. The outcome is already set.
    /// </summary>
    /// <param name="pContext">The context.</param>
    void OnEnd(TaskContext pContext);
}
