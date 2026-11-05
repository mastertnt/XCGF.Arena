namespace XCGF.Arena.Data;

/// <summary>
/// Condition that activates or ends a task.
/// </summary>
public abstract record TriggerDefinition;

/// <summary>
/// Fires as soon as it is evaluated.
/// </summary>
public sealed record ImmediateTrigger : TriggerDefinition;

/// <summary>
/// Fires after a delay. For an activation trigger the delay runs from the assignment of the task, for an exit
/// trigger it runs from the start of the task.
/// </summary>
/// <param name="Delay">The delay.</param>
public sealed record AtTimeTrigger(TimeSpan Delay) : TriggerDefinition;

/// <summary>
/// Fires when another task of the same carrier ends.
/// </summary>
/// <param name="Task">Name of the task.</param>
/// <param name="Outcome">Outcome the task must end with, or null for any outcome.</param>
public sealed record OnTaskEndTrigger(string Task, string? Outcome) : TriggerDefinition;

/// <summary>
/// Fires when the carrier is close enough to a position.
/// </summary>
/// <param name="Parameter">Name of the task parameter holding the position, or null for the first geographic parameter.</param>
/// <param name="Tolerance">Horizontal distance, in metres, under which the position is reached.</param>
public sealed record OnPositionReachedTrigger(string? Parameter, double Tolerance) : TriggerDefinition;

/// <summary>
/// Fires when an event is raised for the task.
/// </summary>
/// <param name="Event">Name of the event.</param>
public sealed record OnEventTrigger(string Event) : TriggerDefinition;

/// <summary>
/// Fires when a condition holds. The expression is evaluated by a handler registered for it.
/// </summary>
/// <param name="Expression">The condition.</param>
public sealed record OnConditionTrigger(string Expression) : TriggerDefinition;

/// <summary>
/// Trigger evaluated by a handler registered for its kind.
/// </summary>
/// <param name="Kind">Kind of the trigger.</param>
/// <param name="Arguments">Arguments of the trigger.</param>
public sealed record CustomTrigger(string Kind, IReadOnlyList<Argument> Arguments) : TriggerDefinition;

/// <summary>
/// Trigger that ends a task with an outcome.
/// </summary>
/// <param name="Outcome">Outcome of the task when the trigger fires, e.g. Success.</param>
/// <param name="Trigger">The condition.</param>
public sealed record ExitTrigger(string Outcome, TriggerDefinition Trigger);
