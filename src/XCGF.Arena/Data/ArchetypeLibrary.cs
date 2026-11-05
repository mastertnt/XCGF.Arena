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
                MergeTasks(lTasks, lDefinition.Tasks);
                lType = lDefinition.Type ?? lType;
                lIsKinematic |= lDefinition.IsKinematic;
            }
        }

        Merge(lTechnological, pEquipment.TechnologicalData);
        Merge(lInitialization, pEquipment.InitializationData);
        MergeTasks(lTasks, pEquipment.Tasks);
        return new ResolvedEquipment(pEquipment.Name, pEquipment.Definition, pEquipment.Type ?? lType, lIsKinematic, lTechnological, lInitialization, lTasks);
    }

    /// <summary>
    /// Parses a value written as text in the library according to its data type.
    /// </summary>
    /// <param name="pDataType">Name of the data type.</param>
    /// <param name="pText">The text.</param>
    /// <returns>The value.</returns>
    public DataValue ParseValue(string pDataType, string pText) => ParseValue(pDataType, ValueNode.Of(pText));

    /// <summary>
    /// Parses a value written in the library according to its data type.
    /// </summary>
    /// <param name="pDataType">Name of the data type.</param>
    /// <param name="pValue">The value: text, fields of a record or items of an array.</param>
    /// <returns>The value.</returns>
    public DataValue ParseValue(string pDataType, ValueNode pValue)
    {
        ArgumentNullException.ThrowIfNull(pValue);
        DataTypeDefinition lType = GetDataType(pDataType);
        switch (lType)
        {
            case BasicDataType lBasic:
                return ParseBasic(lBasic, RequireText(lType, pValue));
            case SimpleDataType lSimple:
                return ParseValue(lSimple.Representation, ValueNode.Of(RequireText(lType, pValue)));
            case EnumeratedDataType lEnumerated:
                string lText = RequireText(lType, pValue);
                Enumerator lEnumerator = lEnumerated.Find(lText)
                    ?? throw new XcgfDataException($"'{lText}' is not an enumerator of '{lEnumerated.Name}'.");
                return new EnumValue(lEnumerator.Name, lEnumerator.Value);
            case ArrayDataType lArray:
                return ParseArray(lArray, pValue);
            case FixedRecordDataType lRecord:
                return ParseRecord(lRecord, pValue);
            case VariantRecordDataType lVariant:
                return ParseVariant(lVariant, pValue);
            default:
                throw new XcgfDataException(
                    $"A default value of type '{lType.Name}' ({lType.GetType().Name}) cannot be written in a library; the scenario must supply it.");
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
        double? lLimit = null;
        if (pBound.Value is not null)
        {
            lLimit = double.TryParse(pBound.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lNumber)
                ? lNumber
                : ParseValue(pItem.DataType, pBound.Value).AsNumber();
        }
        else if (pBound.Reference is not null && pTechnologicalData.TryGetValue(pBound.Reference, out DataValue? lReferenced))
        {
            lLimit = lReferenced?.AsNumber();
        }

        if (lLimit is null)
        {
            return;
        }

        // On a record or an array, the bound applies to every number it holds.
        foreach ((string lPath, double lActual) in NumbersOf(pItem.Name, pValue))
        {
            bool lValid = pBound.Kind == BoundKind.Min
                ? (pBound.Inclusive ? lActual >= lLimit : lActual > lLimit)
                : (pBound.Inclusive ? lActual <= lLimit : lActual < lLimit);
            if (lValid)
            {
                continue;
            }

            string lOperator = (pBound.Kind, pBound.Inclusive) switch
            {
                (BoundKind.Min, true) => ">=",
                (BoundKind.Min, false) => ">",
                (BoundKind.Max, true) => "<=",
                _ => "<",
            };
            string lSource = pBound.Reference is null ? string.Empty : $" ({pBound.Reference})";
            string lShown = lActual.ToString(CultureInfo.InvariantCulture);
            pErrors.Add($"'{lPath}' = {lShown} must be {lOperator} {lLimit.Value.ToString(CultureInfo.InvariantCulture)}{lSource}.");
        }
    }

    private static IEnumerable<(string Path, double Value)> NumbersOf(string pPath, DataValue pValue)
    {
        switch (pValue)
        {
            case RecordValue lRecord:
                return lRecord.Fields.SelectMany(pField => NumbersOf($"{pPath}.{pField.Key}", pField.Value));
            case ArrayValue lArray:
                return lArray.Items.SelectMany((pItem, pIndex) => NumbersOf($"{pPath}[{pIndex}]", pItem));
            case EnumValue:
                return Array.Empty<(string, double)>();
            default:
                return pValue.AsNumber() is { } lNumber ? new[] { (pPath, lNumber) } : Array.Empty<(string, double)>();
        }
    }

    private DataValue ParseArray(ArrayDataType pArray, ValueNode pValue)
    {
        if (pValue.Fields.Count > 0)
        {
            throw new XcgfDataException($"A value of array type '{pArray.Name}' has Item children, not Field children.");
        }

        if (pValue.Items.Count == 0 && pValue.Text.Length > 0)
        {
            // An array of octets written as text is a character string.
            if (GetDataType(pArray.ElementType) is BasicDataType { Size: 8 })
            {
                return new TextValue(pValue.Text);
            }

            throw new XcgfDataException($"A value of array type '{pArray.Name}' must be written as Item children.");
        }

        List<DataValue> lItems = pValue.Items.Select(pItem => ParseValue(pArray.ElementType, pItem)).ToList();
        CheckCardinality(pArray, lItems.Count);
        return new ArrayValue(lItems);
    }

    private RecordValue ParseRecord(FixedRecordDataType pRecord, ValueNode pValue)
    {
        RequireFields(pRecord, pValue);
        Dictionary<string, DataValue> lFields = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, ValueNode> lField in pValue.Fields)
        {
            RecordField lDefinition = pRecord.Fields.FirstOrDefault(pField => pField.Name == lField.Key)
                ?? throw new XcgfDataException($"Record '{pRecord.Name}' has no field '{lField.Key}'.");
            if (!lFields.TryAdd(lField.Key, ParseValue(lDefinition.DataType, lField.Value)))
            {
                throw new XcgfDataException($"Field '{lField.Key}' of record '{pRecord.Name}' is given twice.");
            }
        }

        return new RecordValue(lFields);
    }

    private RecordValue ParseVariant(VariantRecordDataType pVariant, ValueNode pValue)
    {
        RequireFields(pVariant, pValue);
        ValueNode lDiscriminantNode = pValue.Fields.FirstOrDefault(pField => pField.Key == pVariant.Discriminant).Value
            ?? throw new XcgfDataException($"Variant record '{pVariant.Name}' needs its discriminant '{pVariant.Discriminant}'.");
        EnumValue lDiscriminant = (EnumValue)ParseValue(pVariant.DiscriminantType, lDiscriminantNode);
        VariantAlternative? lAlternative = pVariant.Alternatives.FirstOrDefault(pAlternative =>
                pAlternative.Enumerators.Split(',', StringSplitOptions.TrimEntries).Contains(lDiscriminant.Name))
            ?? pVariant.Alternatives.FirstOrDefault(pAlternative => pAlternative.Enumerators.Trim() == "HLAother");
        Dictionary<string, DataValue> lFields = new Dictionary<string, DataValue>(StringComparer.Ordinal) { [pVariant.Discriminant] = lDiscriminant };
        foreach (KeyValuePair<string, ValueNode> lField in pValue.Fields.Where(pField => pField.Key != pVariant.Discriminant))
        {
            if (lAlternative?.Name != lField.Key || lAlternative.DataType is null)
            {
                throw new XcgfDataException($"Field '{lField.Key}' is not the alternative selected by '{lDiscriminant.Name}' in '{pVariant.Name}'.");
            }

            lFields[lField.Key] = ParseValue(lAlternative.DataType, lField.Value);
        }

        return new RecordValue(lFields);
    }

    private static void CheckCardinality(ArrayDataType pArray, int pCount)
    {
        string[] lBounds = pArray.Cardinality.Split("..");
        if (!int.TryParse(lBounds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lMin))
        {
            return;
        }

        int lMax = lBounds.Length == 2 && int.TryParse(lBounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lUpper) ? lUpper : lBounds.Length == 2 ? int.MaxValue : lMin;
        if (pCount < lMin || pCount > lMax)
        {
            throw new XcgfDataException($"Array '{pArray.Name}' holds {pCount} items; its cardinality is {pArray.Cardinality}.");
        }
    }

    private static void RequireFields(DataTypeDefinition pType, ValueNode pValue)
    {
        if (pValue.Items.Count > 0 || pValue.Text.Length > 0)
        {
            throw new XcgfDataException($"A value of record type '{pType.Name}' must be written as Field children.");
        }
    }

    private static string RequireText(DataTypeDefinition pType, ValueNode pValue)
    {
        return pValue.IsText ? pValue.Text : throw new XcgfDataException($"A value of type '{pType.Name}' is written as text, without Field or Item children.");
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

    /// <summary>
    /// Merges task references: a reference to a task already referenced completes or overrides it (description,
    /// parameter defaults and constraints), as a derived definition does for its data items.
    /// </summary>
    /// <param name="pTaskRefs">The references so far, updated in place.</param>
    /// <param name="pOverrides">The references declared next.</param>
    public static void MergeTasks(List<TaskRef> pTaskRefs, IReadOnlyList<TaskRef> pOverrides)
    {
        ArgumentNullException.ThrowIfNull(pTaskRefs);
        ArgumentNullException.ThrowIfNull(pOverrides);
        foreach (TaskRef lOverride in pOverrides)
        {
            int lIndex = pTaskRefs.FindIndex(pRef => pRef.Task == lOverride.Task);
            if (lIndex < 0)
            {
                pTaskRefs.Add(lOverride);
                continue;
            }

            List<DataItem> lParameters = pTaskRefs[lIndex].Parameters.ToList();
            Merge(lParameters, lOverride.Parameters);
            pTaskRefs[lIndex] = new TaskRef(lOverride.Task, lOverride.Description ?? pTaskRefs[lIndex].Description, lParameters);
        }
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
