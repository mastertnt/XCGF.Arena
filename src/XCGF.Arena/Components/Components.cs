using XCGF.Arena.Data;
using XCGF.Arena.Ecs;

namespace XCGF.Arena.Components;

/// <summary>
/// Identity of an entity built from an entity archetype.
/// </summary>
/// <param name="Archetype">Name of the entity archetype.</param>
/// <param name="DisType">Dotted DIS entity type.</param>
/// <param name="Environment">Environment the entity evolves in, if declared.</param>
/// <param name="Folder">Folder of the archetype, if any.</param>
public readonly record struct EntityIdentity(string Archetype, string DisType, EntityEnvironment? Environment, string? Folder);

/// <summary>
/// Kinematic state of an entity.
/// </summary>
/// <param name="Position">Current position.</param>
/// <param name="Orientation">Current attitude. The heading is the direction of travel.</param>
/// <param name="Speed">Current speed, in metres per second.</param>
public record struct Kinematic(GeoPosition Position, Orientation Orientation, double Speed);

/// <summary>
/// Limits the kinematic system enforces, read from the technological data of the kinematic.
/// </summary>
/// <param name="StallSpeed">Lowest speed, in metres per second, or null for none.</param>
/// <param name="MaxSpeed">Highest speed, in metres per second, or null for none.</param>
/// <param name="Ceiling">Highest altitude, in metres, or null for none.</param>
public readonly record struct KinematicLimits(double? StallSpeed, double? MaxSpeed, double? Ceiling);

/// <summary>
/// Marks an entity of the world as an equipment carried by another entity.
/// </summary>
/// <param name="Owner">The carrier.</param>
/// <param name="Name">Name of the equipment on its carrier.</param>
/// <param name="Definition">Name of its definition, if any.</param>
/// <param name="Type">Kind of equipment, e.g. Radar.</param>
/// <param name="IsKinematic">Whether it is the kinematic of its carrier.</param>
public readonly record struct Equipment(Entity Owner, string Name, string? Definition, string? Type, bool IsKinematic);

/// <summary>
/// Data of an equipment.
/// </summary>
/// <param name="Technological">Characteristics.</param>
/// <param name="Initialization">Initial state.</param>
public sealed record EquipmentData(ParameterValues Technological, ParameterValues Initialization);

/// <summary>
/// Equipments carried by an entity, the kinematic included.
/// </summary>
/// <param name="ByName">Equipment entities by equipment name.</param>
public sealed record Equipments(IReadOnlyDictionary<string, Entity> ByName);

/// <summary>
/// Parameters of an entity itself.
/// </summary>
/// <param name="Values">The values.</param>
public sealed record EntityParameters(ParameterValues Values);

/// <summary>
/// Task an entity or a unit can perform.
/// </summary>
/// <param name="Definition">The task.</param>
/// <param name="Defaults">Defaults of the task parameters specific to the carrier.</param>
/// <param name="Provider">Entity that brings the capability: the carrier itself or one of its equipments.</param>
public sealed record TaskCapability(TaskDefinition Definition, IReadOnlyList<DataItem> Defaults, Entity Provider);

/// <summary>
/// Tasks an entity or a unit can perform, its equipments' included.
/// </summary>
/// <param name="ByTask">Capabilities by task name.</param>
public sealed record TaskCapabilities(IReadOnlyDictionary<string, TaskCapability> ByTask);

/// <summary>
/// Member of a unit instance.
/// </summary>
/// <param name="Instance">The entity or unit instance.</param>
/// <param name="Role">Role in the unit, if any.</param>
/// <param name="IsUnit">Whether the member is a unit.</param>
public readonly record struct UnitMemberInstance(Entity Instance, string? Role, bool IsUnit);

/// <summary>
/// Unit built from a unit archetype.
/// </summary>
/// <param name="Archetype">Name of the unit archetype.</param>
/// <param name="Folder">Folder of the archetype, if any.</param>
/// <param name="Members">Members, in archetype order.</param>
public sealed record Unit(string Archetype, string? Folder, IReadOnlyList<UnitMemberInstance> Members);

/// <summary>
/// Membership of an entity or a unit in a unit.
/// </summary>
/// <param name="Unit">The unit.</param>
/// <param name="Role">Role in the unit, if any.</param>
public readonly record struct UnitMembership(Entity Unit, string? Role);
