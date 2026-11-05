using XCGF.Arena.Components;
using XCGF.Arena.Data;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace XCGF.Arena.Systems;

/// <summary>
/// Runs the life cycle of the tasks assigned to entities and units: activation, behaviour, exit.
/// </summary>
/// <remarks>
/// <see cref="ImmediateTrigger"/>, <see cref="AtTimeTrigger"/>, <see cref="OnTaskEndTrigger"/>,
/// <see cref="OnPositionReachedTrigger"/> and <see cref="OnEventTrigger"/> are evaluated by the system.
/// <see cref="OnConditionTrigger"/> and <see cref="CustomTrigger"/> fire only through handlers registered with
/// <see cref="RegisterCondition"/> and <see cref="RegisterCustomTrigger"/>; without a handler they never fire.
/// The system reads the simulation time from <c>Tick.time</c>, the time at the end of the step.
/// </remarks>
public sealed class TaskSystem : QuerySystem<AssignedTasks>
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
    /// <param name="pCarrier">The entity or unit.</param>
    /// <param name="pTask">Name of the task.</param>
    /// <param name="pValues">Scenario values of the parameters, by name.</param>
    /// <returns>The assigned task, pending until its activation trigger fires.</returns>
    /// <exception cref="XcgfDataException">The carrier cannot perform the task, a parameter is unknown, an Out parameter is supplied, a required parameter is missing or a value breaks a constraint.</exception>
    public TaskInstance Assign(Entity pCarrier, string pTask, IReadOnlyDictionary<string, DataValue>? pValues = null)
    {
        if (pCarrier.IsNull || !pCarrier.TryGetComponent(out TaskCapabilities lCapabilities) || !lCapabilities.ByTask.TryGetValue(pTask, out TaskCapability? lCapability))
        {
            throw new XcgfDataException($"{pCarrier} cannot perform task '{pTask}'.");
        }

        TaskDefinition lDefinition = lCapability.Definition;
        Dictionary<string, DataValue?> lParameters = new Dictionary<string, DataValue?>(StringComparer.Ordinal);
        foreach (GroupedTaskParameter lGrouped in lDefinition.Parameters)
        {
            TaskParameter lParameter = lGrouped.Parameter;
            ValueNode? lDefault = lCapability.Defaults.FirstOrDefault(pItem => pItem.Name == lParameter.Name)?.DefaultValue ?? lParameter.DefaultValue;
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

        if (!pCarrier.HasComponent<AssignedTasks>())
        {
            pCarrier.AddComponent(new AssignedTasks());
        }

        TaskInstance lInstance = new TaskInstance(lDefinition, lParameters, mNow);
        pCarrier.GetComponent<AssignedTasks>().Items.Add(lInstance);
        return lInstance;
    }

    /// <summary>
    /// Raises an event for the pending and running tasks of a carrier. It is seen by <see cref="OnEventTrigger"/> at the next step.
    /// </summary>
    /// <param name="pCarrier">The entity or unit.</param>
    /// <param name="pEvent">Name of the event.</param>
    public void RaiseEvent(Entity pCarrier, string pEvent)
    {
        if (!pCarrier.IsNull && pCarrier.TryGetComponent(out AssignedTasks lTasks))
        {
            foreach (TaskInstance lTask in lTasks.Items.Where(pTask => pTask.State != TaskState.Ended))
            {
                lTask.AddEvent(pEvent);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        mNow = TimeSpan.FromSeconds(Tick.time);
        foreach (Entity lCarrier in Query.Entities.ToArray())
        {
            AssignedTasks lTasks = lCarrier.GetComponent<AssignedTasks>();

            // A task that ends during this step can activate a task assigned after it, in the same step.
            foreach (TaskInstance lTask in lTasks.Items.ToList())
            {
                if (lTask.State == TaskState.Ended)
                {
                    continue;
                }

                TaskContext lContext = new TaskContext(lCarrier, lTask, mNow);
                ITaskBehavior? lBehavior = mBehaviors.GetValueOrDefault(lTask.Definition.Name);
                if (lTask.State == TaskState.Pending)
                {
                    if (!Evaluate(lTask.Definition.ActivationTrigger, lContext, lTasks, lTask.AssignedAt))
                    {
                        lTask.ClearEvents();
                        continue;
                    }

                    lTask.State = TaskState.Running;
                    lTask.StartedAt = mNow;
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
                        lTask.EndedAt = mNow;
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
                return pContext.Elapsed - pReference >= lAtTime.Delay;
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
        if (!pContext.Carrier.TryGetComponent(out Kinematic lKinematic))
        {
            return false;
        }

        DataValue? lTarget = pTrigger.Parameter is not null
            ? pContext.Task.Parameters.GetValueOrDefault(pTrigger.Parameter)
            : pContext.Task.Parameters.Values.OfType<RecordValue>().FirstOrDefault(pValue => pValue.Fields.ContainsKey("Latitude") && pValue.Fields.ContainsKey("Longitude"));
        return lTarget is RecordValue && GeoMath.Distance(lKinematic.Position, GeoPosition.FromValue(lTarget)) <= pTrigger.Tolerance;
    }
}
