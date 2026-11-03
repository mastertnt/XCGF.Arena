using Friflo.Engine.ECS;
using XCGF.Arena.Data;

namespace XCGF.Arena.Components;

/// <summary>
/// Identity of an entity built from an entity archetype.
/// </summary>
/// <param name="Archetype">Name of the entity archetype.</param>
/// <param name="DisType">Dotted DIS entity type.</param>
/// <param name="Environment">Environment the entity evolves in, if declared.</param>
/// <param name="Folder">Folder of the archetype, if any.</param>
public record struct EntityIdentity(string Archetype, string DisType, EntityEnvironment? Environment, string? Folder) : IComponent;

/// <summary>
/// Kinematic state of an entity, initialized from the initialization data of its kinematic.
/// </summary>
/// <param name="Position">Current position.</param>
/// <param name="Orientation">Current attitude. The heading is the direction of travel.</param>
/// <param name="Speed">Current speed, in metres per second.</param>
public record struct Kinematic(GeoPosition Position, Orientation Orientation, double Speed) : IComponent;

/// <summary>
/// Limits the kinematic system enforces, read from the technological data of the kinematic.
/// </summary>
/// <param name="StallSpeed">Lowest speed, in metres per second, or null for none.</param>
/// <param name="MaxSpeed">Highest speed, in metres per second, or null for none.</param>
/// <param name="Ceiling">Highest altitude, in metres, or null for none.</param>
public record struct KinematicLimits(double? StallSpeed, double? MaxSpeed, double? Ceiling) : IComponent;

/// <summary>
/// Equipment carried by an entity. The equipment entity is a child of its carrier.
/// </summary>
/// <param name="Name">Name of the equipment on its carrier.</param>
/// <param name="Definition">Name of its definition, if any.</param>
/// <param name="Type">Kind of equipment, e.g. Radar.</param>
/// <param name="IsKinematic">Whether it is the kinematic of its carrier.</param>
public record struct Equipment(string Name, string? Definition, string? Type, bool IsKinematic) : IComponent;

/// <summary>
/// Technological and initialization data of an equipment.
/// </summary>
/// <param name="Technological">Characteristics.</param>
/// <param name="Initialization">Initial state.</param>
public record struct EquipmentData(ParameterValues Technological, ParameterValues Initialization) : IComponent;

/// <summary>
/// Parameters of an entity itself.
/// </summary>
/// <param name="Values">The values.</param>
public record struct EntityParameters(ParameterValues Values) : IComponent;

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
public record struct TaskCapabilities(IReadOnlyDictionary<string, TaskCapability> ByTask) : IComponent;

/// <summary>
/// Unit built from a unit archetype. Its members are its child entities.
/// </summary>
/// <param name="Archetype">Name of the unit archetype.</param>
/// <param name="Folder">Folder of the archetype, if any.</param>
public record struct Unit(string Archetype, string? Folder) : IComponent;

/// <summary>
/// Role of an entity or a unit in the unit that is its parent.
/// </summary>
/// <param name="Role">Role in the unit, if any.</param>
public record struct UnitMembership(string? Role) : IComponent;

/// <summary>
/// Navigation helpers over the entity hierarchy built from the archetypes.
/// </summary>
public static class HierarchyExtensions
{
    /// <summary>
    /// Gets the equipments of an entity, the kinematic included.
    /// </summary>
    /// <param name="pEntity">The carrier.</param>
    /// <returns>The equipment entities, in archetype order.</returns>
    public static IEnumerable<Entity> GetEquipments(this Entity pEntity) => ChildrenWith<Equipment>(pEntity);

    /// <summary>
    /// Finds an equipment of an entity by name. The kinematic is named "Kinematic".
    /// </summary>
    /// <param name="pEntity">The carrier.</param>
    /// <param name="pName">The equipment name.</param>
    /// <returns>The equipment entity, or a null entity when there is none.</returns>
    public static Entity FindEquipment(this Entity pEntity, string pName)
    {
        foreach (Entity lEquipment in pEntity.GetEquipments())
        {
            if (lEquipment.GetComponent<Equipment>().Name == pName)
            {
                return lEquipment;
            }
        }

        return default;
    }

    /// <summary>
    /// Gets the members of a unit.
    /// </summary>
    /// <param name="pUnit">The unit.</param>
    /// <returns>The member entities and units, in archetype order.</returns>
    public static IEnumerable<Entity> GetMembers(this Entity pUnit) => ChildrenWith<UnitMembership>(pUnit);

    /// <summary>
    /// Deletes an entity and, recursively, its children.
    /// </summary>
    /// <param name="pEntity">The entity.</param>
    public static void DeleteTree(this Entity pEntity)
    {
        foreach (Entity lChild in pEntity.ChildEntities.ToArray())
        {
            lChild.DeleteTree();
        }

        pEntity.DeleteEntity();
    }

    private static IEnumerable<Entity> ChildrenWith<T>(Entity pEntity)
        where T : struct, IComponent
    {
        return pEntity.ChildEntities.ToArray().Where(pChild => pChild.HasComponent<T>());
    }
}
