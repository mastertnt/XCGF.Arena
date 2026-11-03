namespace XCGF.Arena.Data;

/// <summary>
/// The 5W groups of task parameters.
/// </summary>
public enum TaskParameterGroup
{
    /// <summary>Who gives the order.</summary>
    WhoTasker,

    /// <summary>Who carries the task out.</summary>
    WhoTaskee,

    /// <summary>Who is affected by the task.</summary>
    WhoAffected,

    /// <summary>What is done.</summary>
    What,

    /// <summary>When it is done.</summary>
    When,

    /// <summary>Where it is done.</summary>
    Where,

    /// <summary>Why it is done.</summary>
    Why,
}

/// <summary>
/// Parameter of a task with the 5W group it belongs to.
/// </summary>
/// <param name="Group">The group.</param>
/// <param name="Parameter">The parameter.</param>
public sealed record GroupedTaskParameter(TaskParameterGroup Group, TaskParameter Parameter);

/// <summary>
/// Task that equipments, entities and units can perform.
/// </summary>
/// <param name="Name">Unique name of the task.</param>
/// <param name="Description">What the task does.</param>
/// <param name="Parameters">Parameters, grouped by the 5W.</param>
/// <param name="ActivationTrigger">Condition that starts the task.</param>
/// <param name="ExitTriggers">Conditions that end the task, evaluated in order.</param>
public sealed record TaskDefinition(
    string Name,
    string? Description,
    IReadOnlyList<GroupedTaskParameter> Parameters,
    TriggerDefinition ActivationTrigger,
    IReadOnlyList<ExitTrigger> ExitTriggers)
{
    /// <summary>
    /// Finds a parameter by name.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The parameter, or null.</returns>
    public GroupedTaskParameter? FindParameter(string pName)
    {
        foreach (GroupedTaskParameter lParameter in Parameters)
        {
            if (lParameter.Parameter.Name == pName)
            {
                return lParameter;
            }
        }

        return null;
    }
}

/// <summary>
/// Reference from an equipment, an entity or a unit to a task it can perform.
/// </summary>
/// <param name="Task">Name of the task.</param>
/// <param name="Description">What the capability adds.</param>
/// <param name="Parameters">Defaults of the task parameters specific to this carrier.</param>
public sealed record TaskRef(string Task, string? Description, IReadOnlyList<DataItem> Parameters);

/// <summary>
/// Equipment or kinematic definition of the equipment library.
/// </summary>
/// <param name="Name">Unique name of the definition.</param>
/// <param name="Base">Name of the definition it derives from, if any.</param>
/// <param name="IsKinematic">Whether it is a kinematic definition.</param>
/// <param name="Type">Kind of equipment, e.g. Radar.</param>
/// <param name="Description">What the equipment is.</param>
/// <param name="TechnologicalData">Characteristics declared by this definition.</param>
/// <param name="InitializationData">Initial state declared by this definition.</param>
/// <param name="Tasks">Tasks the equipment can perform.</param>
public sealed record EquipmentDefinition(
    string Name,
    string? Base,
    bool IsKinematic,
    string? Type,
    string? Description,
    IReadOnlyList<DataItem> TechnologicalData,
    IReadOnlyList<DataItem> InitializationData,
    IReadOnlyList<TaskRef> Tasks);

/// <summary>
/// Equipment carried by an entity archetype. Its content completes or overrides its definition.
/// </summary>
/// <param name="Name">Name of the equipment, unique on its carrier.</param>
/// <param name="Definition">Name of the definition it is built from, if any.</param>
/// <param name="Type">Kind of equipment, overriding the definition's.</param>
/// <param name="Description">What the equipment is.</param>
/// <param name="TechnologicalData">Characteristics overridden or added.</param>
/// <param name="InitializationData">Initial state overridden or added.</param>
/// <param name="Tasks">Tasks added.</param>
public sealed record EquipmentArchetype(
    string Name,
    string? Definition,
    string? Type,
    string? Description,
    IReadOnlyList<DataItem> TechnologicalData,
    IReadOnlyList<DataItem> InitializationData,
    IReadOnlyList<TaskRef> Tasks);

/// <summary>
/// Environment an entity evolves in.
/// </summary>
public enum EntityEnvironment
{
    /// <summary>Other environment.</summary>
    Other,

    /// <summary>On land.</summary>
    Land,

    /// <summary>In the air.</summary>
    Air,

    /// <summary>On the sea surface.</summary>
    Surface,

    /// <summary>Under the sea surface.</summary>
    Subsurface,

    /// <summary>In space.</summary>
    Space,
}

/// <summary>
/// Entity archetype: a platform with a kinematic and equipments.
/// </summary>
/// <param name="Name">Unique name of the archetype.</param>
/// <param name="DisType">Dotted DIS entity type.</param>
/// <param name="Environment">Environment the entity evolves in, if declared.</param>
/// <param name="Folder">Folder the archetype is classified in, if any.</param>
/// <param name="Description">What the entity is.</param>
/// <param name="Kinematic">The mandatory kinematic.</param>
/// <param name="Parameters">Parameters of the entity itself.</param>
/// <param name="Equipments">Other equipments.</param>
/// <param name="Tasks">Tasks the entity can perform.</param>
public sealed record EntityArchetype(
    string Name,
    string DisType,
    EntityEnvironment? Environment,
    string? Folder,
    string? Description,
    EquipmentArchetype Kinematic,
    IReadOnlyList<DataItem> Parameters,
    IReadOnlyList<EquipmentArchetype> Equipments,
    IReadOnlyList<TaskRef> Tasks);

/// <summary>
/// Member of a unit archetype.
/// </summary>
/// <param name="Archetype">Name of the entity or unit archetype.</param>
/// <param name="IsUnit">Whether the member is a unit rather than an entity.</param>
/// <param name="Count">Number of instances of the member.</param>
/// <param name="Role">Role of the member in the unit, if any.</param>
public sealed record UnitMember(string Archetype, bool IsUnit, int Count, string? Role);

/// <summary>
/// Unit archetype: a composition of entity and unit archetypes.
/// </summary>
/// <param name="Name">Unique name of the archetype.</param>
/// <param name="Folder">Folder the archetype is classified in, if any.</param>
/// <param name="Description">What the unit is.</param>
/// <param name="Members">Entity members followed by unit members.</param>
/// <param name="Tasks">Tasks the unit can perform.</param>
public sealed record UnitArchetype(
    string Name,
    string? Folder,
    string? Description,
    IReadOnlyList<UnitMember> Members,
    IReadOnlyList<TaskRef> Tasks);
