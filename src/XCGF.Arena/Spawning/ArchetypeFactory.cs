using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Spawning;

/// <summary>
/// Builds the entities and units of an <see cref="EntityStore"/> from the archetypes of a library.
/// </summary>
/// <remarks>
/// An entity archetype becomes one entity with <see cref="EntityIdentity"/>, <see cref="Kinematic"/>,
/// <see cref="KinematicLimits"/>, <see cref="EntityParameters"/>, <see cref="TaskCapabilities"/> and
/// <see cref="AssignedTasks"/>. Each of its equipments, the kinematic included, becomes a child entity with
/// <see cref="Equipment"/>, <see cref="EquipmentData"/> and its own <see cref="TaskCapabilities"/>. A unit archetype
/// becomes an entity with <see cref="Unit"/> whose members are spawned with it as child entities carrying
/// <see cref="UnitMembership"/>.
/// </remarks>
public sealed class ArchetypeFactory
{
    /// <summary>
    /// Name of the kinematic equipment on every entity.
    /// </summary>
    public const string KinematicEquipment = ArchetypeLibrary.RootKinematic;

    private readonly EntityStore mStore;
    private readonly ArchetypeLibrary mLibrary;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchetypeFactory"/> class.
    /// </summary>
    /// <param name="pStore">The store the instances are created in.</param>
    /// <param name="pLibrary">The archetypes.</param>
    public ArchetypeFactory(EntityStore pStore, ArchetypeLibrary pLibrary)
    {
        ArgumentNullException.ThrowIfNull(pStore);
        ArgumentNullException.ThrowIfNull(pLibrary);
        mStore = pStore;
        mLibrary = pLibrary;
    }

    /// <summary>
    /// Creates an entity from an entity archetype.
    /// </summary>
    /// <param name="pArchetype">Name of the entity archetype.</param>
    /// <param name="pOverrides">
    /// Scenario values that override the defaults. A key "Equipment.Item" sets a technological or initialization data
    /// item of an equipment (the kinematic is named "Kinematic"), a key without a dot sets a parameter of the entity.
    /// </param>
    /// <returns>The entity.</returns>
    /// <exception cref="XcgfDataException">An override is unknown, or a value breaks a constraint.</exception>
    public Entity SpawnEntity(string pArchetype, IReadOnlyDictionary<string, DataValue>? pOverrides = null)
    {
        EntityArchetype lArchetype = mLibrary.GetEntity(pArchetype);
        Dictionary<string, DataValue> lOverrides = pOverrides is null
            ? new Dictionary<string, DataValue>()
            : new Dictionary<string, DataValue>(pOverrides);

        // Build everything before touching the world, so a bad override leaves it unchanged.
        List<(EquipmentArchetype Archetype, ResolvedEquipment Resolved, EquipmentData Data)> lEquipments = new();
        foreach (EquipmentArchetype lEquipment in lArchetype.Equipments.Prepend(lArchetype.Kinematic))
        {
            ResolvedEquipment lResolved = mLibrary.ResolveEquipment(lEquipment);
            EquipmentData lData = BuildEquipmentData(lResolved, lOverrides);
            lEquipments.Add((lEquipment, lResolved, lData));
        }

        ParameterValues lParameters = BuildValues(lArchetype.Parameters, lOverrides, null, $"entity '{lArchetype.Name}'");
        Validate(lParameters, new Dictionary<string, DataValue?>(), $"entity '{lArchetype.Name}'");
        if (lOverrides.Count > 0)
        {
            throw new XcgfDataException(
                $"Entity '{lArchetype.Name}' has no data item {string.Join(", ", lOverrides.Keys.Select(pKey => $"'{pKey}'"))}.");
        }

        Entity lEntity = mStore.CreateEntity();
        Dictionary<string, TaskCapability> lCapabilities = new Dictionary<string, TaskCapability>();
        foreach ((EquipmentArchetype lEquipmentArchetype, ResolvedEquipment lResolved, EquipmentData lData) in lEquipments)
        {
            string lName = lEquipmentArchetype == lArchetype.Kinematic ? KinematicEquipment : lResolved.Name;
            Entity lEquipment = mStore.CreateEntity();
            lEquipment.AddComponent(new Equipment(lName, lResolved.Definition, lResolved.Type, lResolved.IsKinematic));
            lEquipment.AddComponent(lData);
            Dictionary<string, TaskCapability> lEquipmentCapabilities = new Dictionary<string, TaskCapability>();
            AddCapabilities(lEquipmentCapabilities, lResolved.Tasks, lEquipment);
            lEquipment.AddComponent(new TaskCapabilities(lEquipmentCapabilities));
            AddCapabilities(lCapabilities, lResolved.Tasks, lEquipment);
            lEntity.AddChild(lEquipment);
        }

        // The entity's own task references override those its equipments bring.
        AddCapabilities(lCapabilities, lArchetype.Tasks, lEntity);
        EquipmentData lKinematicData = lEquipments[0].Data;
        lEntity.AddComponent(new EntityIdentity(lArchetype.Name, lArchetype.DisType, lArchetype.Environment, lArchetype.Folder));
        lEntity.AddComponent(BuildKinematic(lKinematicData));
        lEntity.AddComponent(new KinematicLimits(
            lKinematicData.Technological.GetNumber("StallSpeed"),
            lKinematicData.Technological.GetNumber("MaxSpeed"),
            lKinematicData.Technological.GetNumber("Ceiling")));
        lEntity.AddComponent(new EntityParameters(lParameters));
        lEntity.AddComponent(new TaskCapabilities(lCapabilities));
        lEntity.AddComponent(new AssignedTasks());
        return lEntity;
    }

    /// <summary>
    /// Creates a unit and all its members, recursively, from a unit archetype.
    /// </summary>
    /// <param name="pArchetype">Name of the unit archetype.</param>
    /// <returns>The unit entity.</returns>
    public Entity SpawnUnit(string pArchetype)
    {
        UnitArchetype lArchetype = mLibrary.GetUnit(pArchetype);
        Entity lUnit = mStore.CreateEntity();
        foreach (UnitMember lMember in lArchetype.Members)
        {
            for (int lIndex = 0; lIndex < lMember.Count; lIndex++)
            {
                Entity lInstance = lMember.IsUnit ? SpawnUnit(lMember.Archetype) : SpawnEntity(lMember.Archetype);
                lInstance.AddComponent(new UnitMembership(lMember.Role));
                lUnit.AddChild(lInstance);
            }
        }

        Dictionary<string, TaskCapability> lCapabilities = new Dictionary<string, TaskCapability>();
        AddCapabilities(lCapabilities, lArchetype.Tasks, lUnit);
        lUnit.AddComponent(new Unit(lArchetype.Name, lArchetype.Folder));
        lUnit.AddComponent(new TaskCapabilities(lCapabilities));
        lUnit.AddComponent(new AssignedTasks());
        return lUnit;
    }

    private EquipmentData BuildEquipmentData(ResolvedEquipment pEquipment, Dictionary<string, DataValue> pOverrides)
    {
        string lPrefix = (pEquipment.IsKinematic ? KinematicEquipment : pEquipment.Name) + ".";
        string lContext = $"equipment '{pEquipment.Name}'";
        ParameterValues lTechnological = BuildValues(pEquipment.TechnologicalData, pOverrides, lPrefix, lContext);
        ParameterValues lInitialization = BuildValues(pEquipment.InitializationData, pOverrides, lPrefix, lContext);
        Validate(lTechnological, lTechnological.Values, lContext);
        Validate(lInitialization, lTechnological.Values, lContext);
        return new EquipmentData(lTechnological, lInitialization);
    }

    private ParameterValues BuildValues(IReadOnlyList<DataItem> pItems, Dictionary<string, DataValue> pOverrides, string? pPrefix, string pContext)
    {
        ParameterValues lValues = new ParameterValues(pItems);
        foreach (DataItem lItem in pItems)
        {
            string lKey = (pPrefix ?? string.Empty) + lItem.Name;
            if (pOverrides.Remove(lKey, out DataValue? lOverride))
            {
                lValues.Set(lItem.Name, lOverride);
            }
            else if (lItem.DefaultValue is not null)
            {
                try
                {
                    lValues.Set(lItem.Name, mLibrary.ParseValue(lItem.DataType, lItem.DefaultValue));
                }
                catch (XcgfDataException lException)
                {
                    throw new XcgfDataException($"Default value of '{lItem.Name}' of {pContext}: {lException.Message}", lException);
                }
            }
        }

        return lValues;
    }

    private void Validate(ParameterValues pValues, IReadOnlyDictionary<string, DataValue?> pTechnological, string pContext)
    {
        List<string> lErrors = new List<string>();
        foreach (KeyValuePair<string, DataValue?> lValue in pValues.Values)
        {
            if (lValue.Value is not null)
            {
                lErrors.AddRange(mLibrary.CheckConstraints(pValues.Items[lValue.Key], lValue.Value, pTechnological));
            }
        }

        if (lErrors.Count > 0)
        {
            throw new XcgfDataException($"Invalid values for {pContext}: {string.Join(" ", lErrors)}");
        }
    }

    private void AddCapabilities(Dictionary<string, TaskCapability> pCapabilities, IReadOnlyList<TaskRef> pTaskRefs, Entity pProvider)
    {
        foreach (TaskRef lTaskRef in pTaskRefs)
        {
            if (pCapabilities.TryGetValue(lTaskRef.Task, out TaskCapability? lExisting))
            {
                // A second reference (the entity's, or another equipment's) completes the first one; the provider
                // stays, since the constraints of its reference are bounded by its own data.
                List<TaskRef> lMerged = new List<TaskRef> { new TaskRef(lTaskRef.Task, null, lExisting.Defaults) };
                ArchetypeLibrary.MergeTasks(lMerged, new[] { lTaskRef });
                pCapabilities[lTaskRef.Task] = lExisting with { Defaults = lMerged[0].Parameters };
            }
            else
            {
                pCapabilities[lTaskRef.Task] = new TaskCapability(mLibrary.GetTask(lTaskRef.Task), lTaskRef.Parameters, pProvider);
            }
        }
    }

    private static Kinematic BuildKinematic(EquipmentData pData)
    {
        ParameterValues lInitialization = pData.Initialization;
        GeoPosition lPosition = lInitialization.Get("Position") is { } lPositionValue ? GeoPosition.FromValue(lPositionValue) : default;
        Orientation lOrientation = lInitialization.Get("Orientation") is { } lOrientationValue ? Orientation.FromValue(lOrientationValue) : default;
        return new Kinematic(lPosition, lOrientation, lInitialization.GetNumber("InitialSpeed") ?? 0);
    }
}
