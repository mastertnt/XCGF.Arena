using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Ecs;

namespace XCGF.Arena.Systems;

/// <summary>
/// Runs the life cycle of the tasks assigned to entities and units: activation, behaviour, exit.
/// </summary>
/// <remarks>
/// <see cref="ImmediateTrigger"/>, <see cref="AtTimeTrigger"/>, <see cref="OnTaskEndTrigger"/>,
/// <see cref="OnPositionReachedTrigger"/> and <see cref="OnEventTrigger"/> are evaluated by the system.
/// <see cref="OnConditionTrigger"/> and <see cref="CustomTrigger"/> fire only through handlers registered with
/// <see cref="RegisterCondition"/> and <see cref="RegisterCustomTrigger"/>; without a handler they never fire.
/// </remarks>
public sealed class TaskSystem : ISystem
{
    private readonly ArchetypeLibrary mLibrary;
    private readonly Dictionary<string, ITaskBehavior> mBehaviors = new Dictionary<string, ITaskBehavior>(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<TaskContext, bool>> mConditions = new Dictionary<string, Func<TaskContext, bool>>(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<TaskContext, CustomTrigger, bool>> mCustomTriggers = new Dictionary<string, Func<TaskContext, CustomTrigger, bool>>(StringComparer.Ordinal);
    private TimeSpan mNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskSystem"/> class, with the built-in behaviours.
    /// </summary>
    /// <param name="pLibrary">The library the tasks come from, used to parse default values.</param>
    public TaskSystem(ArchetypeLibrary pLibrary)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        mLibrary = pLibrary;
        RegisterBehavior(MoveToBehavior.TaskName, new MoveToBehavior());
    }

    /// <summary>
    /// Registers the behaviour of a task, replacing any previous one.
    /// </summary>
    /// <param name="pTask">Name of the task.</param>
    /// <param name="pBehavior">The behaviour.</param>
    public void RegisterBehavior(string pTask, ITaskBehavior pBehavior) => mBehaviors[pTask] = pBehavior ?? throw new ArgumentNullException(nameof(pBehavior));

    /// <summary>
    /// Registers the evaluation of an <see cref="OnConditionTrigger"/> expression.
    /// </summary>
    /// <param name="pExpression">The expression, exactly as written in the library.</param>
    /// <param name="pCondition">The evaluation.</param>
    public void RegisterCondition(string pExpression, Func<TaskContext, bool> pCondition) => mConditions[pExpression] = pCondition ?? throw new ArgumentNullException(nameof(pCondition));

    /// <summary>
    /// Registers the evaluation of a <see cref="CustomTrigger"/> kind.
    /// </summary>
    /// <param name="pKind">The kind.</param>
    /// <param name="pTrigger">The evaluation.</param>
    public void RegisterCustomTrigger(string pKind, Func<TaskContext, CustomTrigger, bool> pTrigger) => mCustomTriggers[pKind] = pTrigger ?? throw new ArgumentNullException(nameof(pTrigger));

    /// <summary>
    /// Assigns a task to an entity or a unit that can perform it.
    /// </summary>
    /// <param name="pWorld">The world.</param>
    /// <param name="pCarrier">The entity or unit.</param>
    /// <param name="pTask">Name of the task.</param>
    /// <param name="pValues">Scenario values of the parameters, by name.</param>
    /// <returns>The assigned task, pending until its activation trigger fires.</returns>
    /// <exception cref="XcgfDataException">The carrier cannot perform the task, a parameter is unknown, an Out parameter is supplied, a required parameter is missing or a value breaks a constraint.</exception>
    public TaskInstance Assign(World pWorld, Entity pCarrier, string pTask, IReadOnlyDictionary<string, DataValue>? pValues = null)
    {
        ArgumentNullException.ThrowIfNull(pWorld);
        if (!pWorld.TryGet(pCarrier, out TaskCapabilities lCapabilities) || !lCapabilities.ByTask.TryGetValue(pTask, out TaskCapability? lCapability))
        {
            throw new XcgfDataException($"{pCarrier} cannot perform task '{pTask}'.");
        }

        TaskDefinition lDefinition = lCapability.Definition;
        Dictionary<string, DataValue?> lParameters = new Dictionary<string, DataValue?>(StringComparer.Ordinal);
        foreach (GroupedTaskParameter lGrouped in lDefinition.Parameters)
        {
            TaskParameter lParameter = lGrouped.Parameter;
            string? lDefault = lCapability.Defaults.FirstOrDefault(pItem => pItem.Name == lParameter.Name)?.DefaultValue ?? lParameter.DefaultValue;
            lParameters[lParameter.Name] = lDefault is null ? null : mLibrary.ParseValue(lParameter.DataType, lDefault);
        }

        foreach (KeyValuePair<string, DataValue> lValue in pValues ?? new Dictionary<string, DataValue>())
        {
            GroupedTaskParameter lGrouped = lDefinition.FindParameter(lValue.Key)
                ?? throw new XcgfDataException($"Task '{pTask}' has no parameter '{lValue.Key}'.");
            if (lGrouped.Parameter.Direction == ParameterDirection.Out)
            {
                throw new XcgfDataException($"Parameter '{lValue.Key}' of task '{pTask}' is set by the task and cannot be supplied.");
            }

            lParameters[lValue.Key] = lValue.Value;
        }

        List<string> lErrors = new List<string>();
        foreach (GroupedTaskParameter lGrouped in lDefinition.Parameters)
        {
            TaskParameter lParameter = lGrouped.Parameter;
            DataValue? lValue = lParameters[lParameter.Name];
            if (lValue is null && lGrouped.Group == TaskParameterGroup.WhoTaskee
                && mLibrary.DataTypes.GetValueOrDefault(lParameter.DataType) is ReferenceDataType { Target: ReferenceTarget.Entity or ReferenceTarget.Unit })
            {
                // The taskee defaults to the carrier of the task.
                lValue = new ReferenceValue(pCarrier);
                lParameters[lParameter.Name] = lValue;
            }

            if (lValue is null)
            {
                if (lParameter.Required && lParameter.Direction != ParameterDirection.Out)
                {
                    lErrors.Add($"Required parameter '{lParameter.Name}' is missing.");
                }

                continue;
            }

            lErrors.AddRange(mLibrary.CheckConstraints(lParameter, lValue, new Dictionary<string, DataValue?>()));
        }

        if (lErrors.Count > 0)
        {
            throw new XcgfDataException($"Cannot assign task '{pTask}': {string.Join(" ", lErrors)}");
        }

        if (!pWorld.Has<AssignedTasks>(pCarrier))
        {
            pWorld.Set(pCarrier, new AssignedTasks());
        }

        TaskInstance lInstance = new TaskInstance(lDefinition, lParameters, mNow);
        pWorld.Get<AssignedTasks>(pCarrier).Items.Add(lInstance);
        return lInstance;
    }

    /// <summary>
    /// Raises an event for the pending and running tasks of a carrier. It is seen by <see cref="OnEventTrigger"/> at the next step.
    /// </summary>
    /// <param name="pWorld">The world.</param>
    /// <param name="pCarrier">The entity or unit.</param>
    /// <param name="pEvent">Name of the event.</param>
    public void RaiseEvent(World pWorld, Entity pCarrier, string pEvent)
    {
        ArgumentNullException.ThrowIfNull(pWorld);
        if (pWorld.TryGet(pCarrier, out AssignedTasks lTasks))
        {
            foreach (TaskInstance lTask in lTasks.Items.Where(pTask => pTask.State != TaskState.Ended))
            {
                lTask.AddEvent(pEvent);
            }
        }
    }

    /// <inheritdoc/>
    public void Update(World pWorld, SimulationTime pTime)
    {
        ArgumentNullException.ThrowIfNull(pWorld);
        mNow = pTime.Elapsed;
        foreach (Entity lCarrier in pWorld.Query<AssignedTasks>())
        {
            AssignedTasks lTasks = pWorld.Get<AssignedTasks>(lCarrier);

            // A task that ends during this step can activate a task assigned after it, in the same step.
            foreach (TaskInstance lTask in lTasks.Items.ToList())
            {
                if (lTask.State == TaskState.Ended)
                {
                    continue;
                }

                TaskContext lContext = new TaskContext(pWorld, lCarrier, lTask, pTime);
                ITaskBehavior? lBehavior = mBehaviors.GetValueOrDefault(lTask.Definition.Name);
                if (lTask.State == TaskState.Pending)
                {
                    if (!Evaluate(lTask.Definition.ActivationTrigger, lContext, lTasks, lTask.AssignedAt))
                    {
                        lTask.ClearEvents();
                        continue;
                    }

                    lTask.State = TaskState.Running;
                    lTask.StartedAt = pTime.Elapsed;
                    lBehavior?.OnStart(lContext);
                }
                else
                {
                    lBehavior?.OnUpdate(lContext);
                }

                foreach (ExitTrigger lExit in lTask.Definition.ExitTriggers)
                {
                    if (Evaluate(lExit.Trigger, lContext, lTasks, lTask.StartedAt!.Value))
                    {
                        lTask.State = TaskState.Ended;
                        lTask.EndedAt = pTime.Elapsed;
                        lTask.Outcome = lExit.Outcome;
                        lBehavior?.OnEnd(lContext);
                        break;
                    }
                }

                lTask.ClearEvents();
            }
        }
    }

    private bool Evaluate(TriggerDefinition pTrigger, TaskContext pContext, AssignedTasks pTasks, TimeSpan pReference)
    {
        switch (pTrigger)
        {
            case ImmediateTrigger:
                return true;
            case AtTimeTrigger lAtTime:
                return pContext.Time.Elapsed - pReference >= lAtTime.Delay;
            case OnTaskEndTrigger lOnTaskEnd:
                return pTasks.Items.Any(pTask =>
                    pTask != pContext.Task
                    && pTask.State == TaskState.Ended
                    && pTask.Definition.Name == lOnTaskEnd.Task
                    && pTask.EndedAt >= pContext.Task.AssignedAt
                    && (lOnTaskEnd.Outcome is null || pTask.Outcome == lOnTaskEnd.Outcome));
            case OnPositionReachedTrigger lOnPosition:
                return IsPositionReached(lOnPosition, pContext);
            case OnEventTrigger lOnEvent:
                return pContext.Task.Events.Contains(lOnEvent.Event);
            case OnConditionTrigger lOnCondition:
                return mConditions.TryGetValue(lOnCondition.Expression, out Func<TaskContext, bool>? lCondition) && lCondition(pContext);
            case CustomTrigger lCustom:
                return mCustomTriggers.TryGetValue(lCustom.Kind, out Func<TaskContext, CustomTrigger, bool>? lHandler) && lHandler(pContext, lCustom);
            default:
                return false;
        }
    }

    private static bool IsPositionReached(OnPositionReachedTrigger pTrigger, TaskContext pContext)
    {
        if (!pContext.World.TryGet(pContext.Carrier, out Kinematic lKinematic))
        {
            return false;
        }

        DataValue? lTarget = pTrigger.Parameter is not null
            ? pContext.Task.Parameters.GetValueOrDefault(pTrigger.Parameter)
            : pContext.Task.Parameters.Values.OfType<RecordValue>().FirstOrDefault(pValue => pValue.Fields.ContainsKey("Latitude") && pValue.Fields.ContainsKey("Longitude"));
        return lTarget is RecordValue && GeoMath.Distance(lKinematic.Position, GeoPosition.FromValue(lTarget)) <= pTrigger.Tolerance;
    }
}
