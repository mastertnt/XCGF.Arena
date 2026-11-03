using System.Globalization;
using System.Text.RegularExpressions;

namespace XCGF.Arena.Data;

/// <summary>
/// Archetypes of an XCGF.Data library: data types, equipment definitions, tasks, entities and units.
/// </summary>
public sealed class ArchetypeLibrary
{
    /// <summary>
    /// Name of the root kinematic definition every kinematic derives from.
    /// </summary>
    public const string RootKinematic = "Kinematic";

    private readonly Dictionary<string, DataTypeDefinition> mDataTypes = new Dictionary<string, DataTypeDefinition>();
    private readonly Dictionary<string, EquipmentDefinition> mEquipmentDefinitions = new Dictionary<string, EquipmentDefinition>();
    private readonly Dictionary<string, TaskDefinition> mTasks = new Dictionary<string, TaskDefinition>();
    private readonly Dictionary<string, EntityArchetype> mEntities = new Dictionary<string, EntityArchetype>();
    private readonly Dictionary<string, UnitArchetype> mUnits = new Dictionary<string, UnitArchetype>();

    /// <summary>
    /// Gets the data types, by name.
    /// </summary>
    public IReadOnlyDictionary<string, DataTypeDefinition> DataTypes => mDataTypes;

    /// <summary>
    /// Gets the equipment and kinematic definitions, by name.
    /// </summary>
    public IReadOnlyDictionary<string, EquipmentDefinition> EquipmentDefinitions => mEquipmentDefinitions;

    /// <summary>
    /// Gets the task definitions, by name.
    /// </summary>
    public IReadOnlyDictionary<string, TaskDefinition> Tasks => mTasks;

    /// <summary>
    /// Gets the entity archetypes, by name.
    /// </summary>
    public IReadOnlyDictionary<string, EntityArchetype> Entities => mEntities;

    /// <summary>
    /// Gets the unit archetypes, by name.
    /// </summary>
    public IReadOnlyDictionary<string, UnitArchetype> Units => mUnits;

    /// <summary>
    /// Adds a data type.
    /// </summary>
    /// <param name="pDataType">The data type.</param>
    public void Add(DataTypeDefinition pDataType) => AddUnique(mDataTypes, pDataType.Name, pDataType, "data type");

    /// <summary>
    /// Adds an equipment or kinematic definition.
    /// </summary>
    /// <param name="pDefinition">The definition.</param>
    public void Add(EquipmentDefinition pDefinition) => AddUnique(mEquipmentDefinitions, pDefinition.Name, pDefinition, "equipment definition");

    /// <summary>
    /// Adds a task definition.
    /// </summary>
    /// <param name="pTask">The task.</param>
    public void Add(TaskDefinition pTask) => AddUnique(mTasks, pTask.Name, pTask, "task");

    /// <summary>
    /// Adds an entity archetype.
    /// </summary>
    /// <param name="pEntity">The archetype.</param>
    public void Add(EntityArchetype pEntity) => AddUnique(mEntities, pEntity.Name, pEntity, "entity");

    /// <summary>
    /// Adds a unit archetype.
    /// </summary>
    /// <param name="pUnit">The archetype.</param>
    public void Add(UnitArchetype pUnit) => AddUnique(mUnits, pUnit.Name, pUnit, "unit");

    /// <summary>
    /// Gets a data type by name.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The data type.</returns>
    public DataTypeDefinition GetDataType(string pName) => GetRequired(mDataTypes, pName, "data type");

    /// <summary>
    /// Gets a task by name.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The task.</returns>
    public TaskDefinition GetTask(string pName) => GetRequired(mTasks, pName, "task");

    /// <summary>
    /// Gets an entity archetype by name.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The archetype.</returns>
    public EntityArchetype GetEntity(string pName) => GetRequired(mEntities, pName, "entity");

    /// <summary>
    /// Gets a unit archetype by name.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The archetype.</returns>
    public UnitArchetype GetUnit(string pName) => GetRequired(mUnits, pName, "unit");

    /// <summary>
    /// Gets the derivation chain of a definition, from the root to the definition itself.
    /// </summary>
    /// <param name="pName">Name of the definition.</param>
    /// <returns>The chain.</returns>
    public IReadOnlyList<EquipmentDefinition> GetDerivationChain(string pName)
    {
        List<EquipmentDefinition> lChain = new List<EquipmentDefinition>();
        HashSet<string> lVisited = new HashSet<string>();
        string? lCurrent = pName;
        while (lCurrent is not null)
        {
            if (!lVisited.Add(lCurrent))
            {
                throw new XcgfDataException($"Equipment definition '{pName}' derives from itself through '{lCurrent}'.");
            }

            EquipmentDefinition lDefinition = GetRequired(mEquipmentDefinitions, lCurrent, "equipment definition");
            lChain.Add(lDefinition);
            lCurrent = lDefinition.Base;
        }

        lChain.Reverse();
        return lChain;
    }

    /// <summary>
    /// Resolves an equipment of an entity archetype: its definition chain merged with its own content.
    /// </summary>
    /// <param name="pEquipment">The equipment.</param>
    /// <returns>The resolved equipment.</returns>
    public ResolvedEquipment ResolveEquipment(EquipmentArchetype pEquipment)
    {
        ArgumentNullException.ThrowIfNull(pEquipment);
        List<DataItem> lTechnological = new List<DataItem>();
        List<DataItem> lInitialization = new List<DataItem>();
        List<TaskRef> lTasks = new List<TaskRef>();
        string? lType = null;
        bool lIsKinematic = false;
        if (pEquipment.Definition is not null)
        {
            foreach (EquipmentDefinition lDefinition in GetDerivationChain(pEquipment.Definition))
            {
                Merge(lTechnological, lDefinition.TechnologicalData);
                Merge(lInitialization, lDefinition.InitializationData);
                lTasks.AddRange(lDefinition.Tasks);
                lType = lDefinition.Type ?? lType;
                lIsKinematic |= lDefinition.IsKinematic;
            }
        }

        Merge(lTechnological, pEquipment.TechnologicalData);
        Merge(lInitialization, pEquipment.InitializationData);
        lTasks.AddRange(pEquipment.Tasks);
        return new ResolvedEquipment(pEquipment.Name, pEquipment.Definition, pEquipment.Type ?? lType, lIsKinematic, lTechnological, lInitialization, lTasks);
    }

    /// <summary>
    /// Parses a value written in the library according to its data type.
    /// </summary>
    /// <param name="pDataType">Name of the data type.</param>
    /// <param name="pText">The text.</param>
    /// <returns>The value.</returns>
    public DataValue ParseValue(string pDataType, string pText)
    {
        ArgumentNullException.ThrowIfNull(pText);
        DataTypeDefinition lType = GetDataType(pDataType);
        string lText = pText.Trim();
        switch (lType)
        {
            case BasicDataType lBasic:
                return ParseBasic(lBasic, lText);
            case SimpleDataType lSimple:
                return ParseValue(lSimple.Representation, lText);
            case EnumeratedDataType lEnumerated:
                Enumerator lEnumerator = lEnumerated.Find(lText)
                    ?? throw new XcgfDataException($"'{lText}' is not an enumerator of '{lEnumerated.Name}'.");
                return new EnumValue(lEnumerator.Name, lEnumerator.Value);
            case ArrayDataType lArray when GetDataType(lArray.ElementType) is BasicDataType { Size: 8 }:
                return new TextValue(pText);
            default:
                throw new XcgfDataException(
                    $"A default value of type '{lType.Name}' ({lType.GetType().Name}) cannot be written as text; the scenario must supply it.");
        }
    }

    /// <summary>
    /// Checks a value against the constraints of its data item.
    /// </summary>
    /// <param name="pItem">The data item.</param>
    /// <param name="pValue">The value.</param>
    /// <param name="pTechnologicalData">Values of the technological data the bounds may reference.</param>
    /// <returns>The violated constraints, described; empty when the value is valid.</returns>
    public IReadOnlyList<string> CheckConstraints(DataItem pItem, DataValue pValue, IReadOnlyDictionary<string, DataValue?> pTechnologicalData)
    {
        ArgumentNullException.ThrowIfNull(pItem);
        ArgumentNullException.ThrowIfNull(pValue);
        List<string> lErrors = new List<string>();
        foreach (DataConstraint lConstraint in pItem.Constraints)
        {
            switch (lConstraint)
            {
                case BoundConstraint lBound:
                    CheckBound(pItem, lBound, pValue, pTechnologicalData, lErrors);
                    break;
                case ListOfValuesConstraint lList when !lList.Values.Contains(pValue.ToString()):
                    lErrors.Add($"'{pItem.Name}' = {pValue} is not one of {string.Join(", ", lList.Values)}.");
                    break;
                case PatternConstraint lPattern when !Regex.IsMatch(pValue.ToString() ?? string.Empty, "^(?:" + lPattern.Pattern + ")$"):
                    lErrors.Add($"'{pItem.Name}' = {pValue} does not match '{lPattern.Pattern}'.");
                    break;
            }
        }

        return lErrors;
    }

    /// <summary>
    /// Checks the cross references and the rules XSD 1.0 cannot express.
    /// </summary>
    /// <returns>The problems found; empty when the library is consistent.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> lErrors = new List<string>();
        foreach (DataTypeDefinition lType in mDataTypes.Values)
        {
            foreach (string lReferenced in ReferencedTypes(lType))
            {
                RequireType(lReferenced, $"data type '{lType.Name}'", lErrors);
            }
        }

        foreach (EquipmentDefinition lDefinition in mEquipmentDefinitions.Values)
        {
            string lContext = $"equipment definition '{lDefinition.Name}'";
            Try(() =>
            {
                IReadOnlyList<EquipmentDefinition> lChain = GetDerivationChain(lDefinition.Name);
                if (lDefinition.IsKinematic && lChain[0].Name != RootKinematic)
                {
                    lErrors.Add($"Kinematic definition '{lDefinition.Name}' does not derive from '{RootKinematic}'.");
                }
            }, lErrors);
            ValidateItems(lDefinition.TechnologicalData, lContext, lErrors);
            ValidateItems(lDefinition.InitializationData, lContext, lErrors);
            ValidateTaskRefs(lDefinition.Tasks, lContext, lErrors);
        }

        foreach (TaskDefinition lTask in mTasks.Values)
        {
            ValidateItems(lTask.Parameters.Select(pParameter => (DataItem)pParameter.Parameter).ToList(), $"task '{lTask.Name}'", lErrors);
            foreach (TriggerDefinition lTrigger in lTask.ExitTriggers.Select(pExit => pExit.Trigger).Append(lTask.ActivationTrigger))
            {
                if (lTrigger is OnTaskEndTrigger lOnTaskEnd && !mTasks.ContainsKey(lOnTaskEnd.Task))
                {
                    lErrors.Add($"Task '{lTask.Name}' waits for unknown task '{lOnTaskEnd.Task}'.");
                }
                else if (lTrigger is OnPositionReachedTrigger { Parameter: { } lParameter } && lTask.FindParameter(lParameter) is null)
                {
                    lErrors.Add($"Task '{lTask.Name}' waits for a position in unknown parameter '{lParameter}'.");
                }
            }
        }

        foreach (EntityArchetype lEntity in mEntities.Values)
        {
            ValidateEntity(lEntity, lErrors);
        }

        foreach (UnitArchetype lUnit in mUnits.Values)
        {
            ValidateTaskRefs(lUnit.Tasks, $"unit '{lUnit.Name}'", lErrors);
            foreach (UnitMember lMember in lUnit.Members)
            {
                bool lExists = lMember.IsUnit ? mUnits.ContainsKey(lMember.Archetype) : mEntities.ContainsKey(lMember.Archetype);
                if (!lExists)
                {
                    lErrors.Add($"Unit '{lUnit.Name}' references unknown {(lMember.IsUnit ? "unit" : "entity")} '{lMember.Archetype}'.");
                }
            }

            if (FindUnitCycle(lUnit.Name, new List<string>()) is { } lCycle)
            {
                lErrors.Add($"Unit '{lUnit.Name}' contains itself: {string.Join(" > ", lCycle)}.");
            }
        }

        return lErrors;
    }

    private void ValidateEntity(EntityArchetype pEntity, List<string> pErrors)
    {
        string lContext = $"entity '{pEntity.Name}'";
        ValidateItems(pEntity.Parameters, lContext, pErrors);
        ValidateTaskRefs(pEntity.Tasks, lContext, pErrors);
        foreach (EquipmentArchetype lEquipment in pEntity.Equipments.Prepend(pEntity.Kinematic))
        {
            string lEquipmentContext = $"equipment '{lEquipment.Name}' of {lContext}";
            ValidateItems(lEquipment.TechnologicalData, lEquipmentContext, pErrors);
            ValidateItems(lEquipment.InitializationData, lEquipmentContext, pErrors);
            ValidateTaskRefs(lEquipment.Tasks, lEquipmentContext, pErrors);
            Try(() =>
            {
                ResolvedEquipment lResolved = ResolveEquipment(lEquipment);
                ValidateResolved(lResolved, lEquipmentContext, pErrors);
            }, pErrors);
        }

        Try(() =>
        {
            ResolvedEquipment lKinematic = ResolveEquipment(pEntity.Kinematic);
            if (!lKinematic.IsKinematic)
            {
                pErrors.Add($"The kinematic of {lContext} references '{pEntity.Kinematic.Definition}', which is not a kinematic definition.");
            }

            foreach (string lRequired in new[] { "Position", "Orientation" })
            {
                if (lKinematic.InitializationData.All(pItem => pItem.Name != lRequired))
                {
                    pErrors.Add($"The kinematic of {lContext} declares no '{lRequired}'.");
                }
            }
        }, pErrors);
    }

    private void ValidateResolved(ResolvedEquipment pEquipment, string pContext, List<string> pErrors)
    {
        HashSet<string> lTechnologicalNames = pEquipment.TechnologicalData.Select(pItem => pItem.Name).ToHashSet();
        foreach (DataItem lItem in pEquipment.TechnologicalData.Concat(pEquipment.InitializationData))
        {
            foreach (BoundConstraint lBound in lItem.Constraints.OfType<BoundConstraint>())
            {
                if ((lBound.Value is null) == (lBound.Reference is null))
                {
                    pErrors.Add($"A {lBound.Kind} of '{lItem.Name}' in {pContext} must hold either a value or a ref.");
                }
                else if (lBound.Reference is not null && !lTechnologicalNames.Contains(lBound.Reference))
                {
                    pErrors.Add($"A {lBound.Kind} of '{lItem.Name}' in {pContext} references unknown technological data '{lBound.Reference}'.");
                }
            }
        }
    }

    private void ValidateItems(IReadOnlyList<DataItem> pItems, string pContext, List<string> pErrors)
    {
        foreach (DataItem lItem in pItems)
        {
            if (RequireType(lItem.DataType, $"'{lItem.Name}' of {pContext}", pErrors) && lItem.DefaultValue is not null)
            {
                Try(() => ParseValue(lItem.DataType, lItem.DefaultValue), pErrors, $"Default value of '{lItem.Name}' of {pContext}: ");
            }
        }
    }

    private void ValidateTaskRefs(IReadOnlyList<TaskRef> pTaskRefs, string pContext, List<string> pErrors)
    {
        foreach (TaskRef lTaskRef in pTaskRefs)
        {
            if (!mTasks.TryGetValue(lTaskRef.Task, out TaskDefinition? lTask))
            {
                pErrors.Add($"{pContext} references unknown task '{lTaskRef.Task}'.");
                continue;
            }

            foreach (DataItem lParameter in lTaskRef.Parameters)
            {
                if (lTask.FindParameter(lParameter.Name) is null)
                {
                    pErrors.Add($"{pContext} sets unknown parameter '{lParameter.Name}' of task '{lTask.Name}'.");
                }
            }

            ValidateItems(lTaskRef.Parameters, $"task '{lTask.Name}' of {pContext}", pErrors);
        }
    }

    private bool RequireType(string pName, string pContext, List<string> pErrors)
    {
        if (mDataTypes.ContainsKey(pName))
        {
            return true;
        }

        pErrors.Add($"{pContext} uses unknown data type '{pName}'.");
        return false;
    }

    private List<string>? FindUnitCycle(string pName, List<string> pPath)
    {
        if (pPath.Contains(pName))
        {
            return pPath.Append(pName).ToList();
        }

        if (!mUnits.TryGetValue(pName, out UnitArchetype? lUnit))
        {
            return null;
        }

        pPath.Add(pName);
        foreach (UnitMember lMember in lUnit.Members.Where(pMember => pMember.IsUnit))
        {
            if (FindUnitCycle(lMember.Archetype, pPath) is { } lCycle)
            {
                return lCycle;
            }
        }

        pPath.RemoveAt(pPath.Count - 1);
        return null;
    }

    private void CheckBound(DataItem pItem, BoundConstraint pBound, DataValue pValue, IReadOnlyDictionary<string, DataValue?> pTechnologicalData, List<string> pErrors)
    {
        double? lActual = pValue.AsNumber();
        double? lLimit = null;
        if (pBound.Value is not null)
        {
            lLimit = ParseValue(pItem.DataType, pBound.Value).AsNumber();
        }
        else if (pBound.Reference is not null && pTechnologicalData.TryGetValue(pBound.Reference, out DataValue? lReferenced))
        {
            lLimit = lReferenced?.AsNumber();
        }

        if (lActual is null || lLimit is null)
        {
            return;
        }

        bool lValid = pBound.Kind == BoundKind.Min
            ? (pBound.Inclusive ? lActual >= lLimit : lActual > lLimit)
            : (pBound.Inclusive ? lActual <= lLimit : lActual < lLimit);
        if (!lValid)
        {
            string lOperator = (pBound.Kind, pBound.Inclusive) switch
            {
                (BoundKind.Min, true) => ">=",
                (BoundKind.Min, false) => ">",
                (BoundKind.Max, true) => "<=",
                _ => "<",
            };
            string lSource = pBound.Reference is null ? string.Empty : $" ({pBound.Reference})";
            pErrors.Add($"'{pItem.Name}' = {pValue} must be {lOperator} {lLimit.Value.ToString(CultureInfo.InvariantCulture)}{lSource}.");
        }
    }

    private static DataValue ParseBasic(BasicDataType pType, string pText)
    {
        if (pType.IsFloatingPoint)
        {
            if (double.TryParse(pText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lNumber))
            {
                return new NumberValue(lNumber);
            }
        }
        else if (long.TryParse(pText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lInteger))
        {
            return new IntegerValue(lInteger);
        }

        throw new XcgfDataException($"'{pText}' is not a valid '{pType.Name}'.");
    }

    private static IEnumerable<string> ReferencedTypes(DataTypeDefinition pType)
    {
        return pType switch
        {
            SimpleDataType lSimple => new[] { lSimple.Representation },
            EnumeratedDataType lEnumerated => new[] { lEnumerated.Representation },
            ArrayDataType lArray => new[] { lArray.ElementType },
            FixedRecordDataType lRecord => lRecord.Fields.Select(pField => pField.DataType),
            VariantRecordDataType lVariant => lVariant.Alternatives
                .Where(pAlternative => pAlternative.DataType is not null)
                .Select(pAlternative => pAlternative.DataType!)
                .Prepend(lVariant.DiscriminantType),
            _ => Array.Empty<string>(),
        };
    }

    private static void Merge(List<DataItem> pItems, IReadOnlyList<DataItem> pOverrides)
    {
        foreach (DataItem lOverride in pOverrides)
        {
            int lIndex = pItems.FindIndex(pItem => pItem.Name == lOverride.Name);
            if (lIndex < 0)
            {
                pItems.Add(lOverride);
            }
            else
            {
                pItems[lIndex] = pItems[lIndex].MergeWith(lOverride);
            }
        }
    }

    private static void Try(Action pAction, List<string> pErrors, string pPrefix = "")
    {
        try
        {
            pAction();
        }
        catch (XcgfDataException lException)
        {
            pErrors.Add(pPrefix + lException.Message);
        }
    }

    private static void AddUnique<T>(Dictionary<string, T> pItems, string pName, T pItem, string pKind)
    {
        ArgumentNullException.ThrowIfNull(pItem);
        if (!pItems.TryAdd(pName, pItem))
        {
            throw new XcgfDataException($"The {pKind} '{pName}' is declared twice.");
        }
    }

    private static T GetRequired<T>(Dictionary<string, T> pItems, string pName, string pKind)
    {
        return pItems.TryGetValue(pName, out T? lItem) ? lItem : throw new XcgfDataException($"Unknown {pKind} '{pName}'.");
    }
}

/// <summary>
/// Equipment once its definition chain and its own content are merged.
/// </summary>
/// <param name="Name">Name of the equipment on its carrier.</param>
/// <param name="Definition">Name of its definition, if any.</param>
/// <param name="Type">Kind of equipment.</param>
/// <param name="IsKinematic">Whether it is the kinematic of its carrier.</param>
/// <param name="TechnologicalData">Characteristics.</param>
/// <param name="InitializationData">Initial state.</param>
/// <param name="Tasks">Tasks it can perform.</param>
public sealed record ResolvedEquipment(
    string Name,
    string? Definition,
    string? Type,
    bool IsKinematic,
    IReadOnlyList<DataItem> TechnologicalData,
    IReadOnlyList<DataItem> InitializationData,
    IReadOnlyList<TaskRef> Tasks);
