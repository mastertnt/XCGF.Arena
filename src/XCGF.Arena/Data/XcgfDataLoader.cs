using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace XCGF.Arena.Data;

/// <summary>
/// Reads XCGF.Data XML documents (namespace <c>urn:xcgf:data:1.0</c>) into an <see cref="ArchetypeLibrary"/>.
/// </summary>
/// <remarks>
/// The loader resolves <c>xi:include</c> elements itself, since .NET does not implement XInclude. Any global element
/// of the schema can be the root of a document: XcgfData, DataTypes, EquipmentLibrary, TaskLibrary, Entities,
/// Entity or Units. It does not validate against the XSD; it checks the cross references once everything is loaded.
/// </remarks>
public static class XcgfDataLoader
{
    /// <summary>
    /// The XCGF.Data namespace.
    /// </summary>
    public static readonly XNamespace Namespace = "urn:xcgf:data:1.0";

    private static readonly XNamespace sXInclude = "http://www.w3.org/2001/XInclude";
    private const int MaxIncludeDepth = 32;

    /// <summary>
    /// Loads one or several files into a new library and checks it.
    /// </summary>
    /// <param name="pPaths">The files. Included files are found relative to the file that includes them.</param>
    /// <returns>The library.</returns>
    /// <exception cref="XcgfDataException">A file is malformed or the library is inconsistent.</exception>
    public static ArchetypeLibrary Load(params string[] pPaths)
    {
        ArchetypeLibrary lLibrary = new ArchetypeLibrary();
        foreach (string lPath in pPaths)
        {
            LoadInto(lLibrary, lPath);
        }

        EnsureValid(lLibrary);
        return lLibrary;
    }

    /// <summary>
    /// Adds the archetypes of a file to a library, without checking the library.
    /// </summary>
    /// <param name="pLibrary">The library.</param>
    /// <param name="pPath">The file.</param>
    public static void LoadInto(ArchetypeLibrary pLibrary, string pPath)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        ArgumentNullException.ThrowIfNull(pPath);
        XElement lRoot = LoadResolved(Path.GetFullPath(pPath), new Stack<string>());
        ReadRoot(pLibrary, lRoot);
    }

    /// <summary>
    /// Adds the archetypes of an in-memory document to a library, without checking the library.
    /// </summary>
    /// <param name="pLibrary">The library.</param>
    /// <param name="pDocument">The document. Relative includes are resolved against <paramref name="pBaseDirectory"/>.</param>
    /// <param name="pBaseDirectory">Directory of the document, or null when it has no include.</param>
    public static void LoadInto(ArchetypeLibrary pLibrary, XDocument pDocument, string? pBaseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        ArgumentNullException.ThrowIfNull(pDocument);
        XElement lRoot = pDocument.Root ?? throw new XcgfDataException("The document is empty.");
        XElement lCopy = new XElement(lRoot);
        ResolveIncludes(lCopy, pBaseDirectory ?? Directory.GetCurrentDirectory(), new Stack<string>());
        ReadRoot(pLibrary, lCopy);
    }

    /// <summary>
    /// Throws when a library is inconsistent.
    /// </summary>
    /// <param name="pLibrary">The library.</param>
    public static void EnsureValid(ArchetypeLibrary pLibrary)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        IReadOnlyList<string> lErrors = pLibrary.Validate();
        if (lErrors.Count > 0)
        {
            throw new XcgfDataException("The library is inconsistent:" + Environment.NewLine + string.Join(Environment.NewLine, lErrors));
        }
    }

    private static XElement LoadResolved(string pPath, Stack<string> pIncluding)
    {
        if (pIncluding.Contains(pPath, StringComparer.Ordinal))
        {
            throw new XcgfDataException($"'{pPath}' includes itself.");
        }

        if (pIncluding.Count >= MaxIncludeDepth)
        {
            throw new XcgfDataException($"Includes are nested deeper than {MaxIncludeDepth} levels at '{pPath}'.");
        }

        XDocument lDocument;
        try
        {
            XmlReaderSettings lSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using XmlReader lReader = XmlReader.Create(pPath, lSettings);
            lDocument = XDocument.Load(lReader, LoadOptions.SetLineInfo);
        }
        catch (Exception lException) when (lException is IOException or XmlException or UnauthorizedAccessException)
        {
            throw new XcgfDataException($"Cannot read '{pPath}': {lException.Message}", lException);
        }

        XElement lRoot = lDocument.Root ?? throw new XcgfDataException($"'{pPath}' is empty.");
        pIncluding.Push(pPath);
        ResolveIncludes(lRoot, Path.GetDirectoryName(pPath)!, pIncluding);
        pIncluding.Pop();
        return lRoot;
    }

    private static void ResolveIncludes(XElement pElement, string pDirectory, Stack<string> pIncluding)
    {
        foreach (XElement lInclude in pElement.Descendants(sXInclude + "include").ToList())
        {
            string lHref = (string?)lInclude.Attribute("href") ?? throw Error(lInclude, "xi:include has no href.");
            string? lParse = (string?)lInclude.Attribute("parse");
            if ((lParse is not null && lParse != "xml") || lInclude.Attribute("xpointer") is not null)
            {
                throw Error(lInclude, "Only whole-document xi:include with parse=\"xml\" is supported.");
            }

            XElement lIncluded = LoadResolved(Path.GetFullPath(Path.Combine(pDirectory, lHref)), pIncluding);
            lInclude.ReplaceWith(lIncluded);
        }
    }

    private static void ReadRoot(ArchetypeLibrary pLibrary, XElement pRoot)
    {
        if (pRoot.Name.Namespace != Namespace)
        {
            throw Error(pRoot, $"The root element is not in the '{Namespace}' namespace.");
        }

        switch (pRoot.Name.LocalName)
        {
            case "XcgfData":
                foreach (XElement lSection in pRoot.Elements().Where(pElement => pElement.Name != Namespace + "Description"))
                {
                    ReadRoot(pLibrary, lSection);
                }

                break;
            case "DataTypes":
                ReadDataTypes(pLibrary, pRoot);
                break;
            case "EquipmentLibrary":
                ReadEquipmentLibrary(pLibrary, pRoot);
                break;
            case "TaskLibrary":
                foreach (XElement lTask in pRoot.Elements(Namespace + "TaskDefinition"))
                {
                    pLibrary.Add(ReadTask(lTask));
                }

                break;
            case "Entities":
                foreach (XElement lEntity in pRoot.Elements(Namespace + "Entity"))
                {
                    pLibrary.Add(ReadEntity(lEntity));
                }

                break;
            case "Entity":
                pLibrary.Add(ReadEntity(pRoot));
                break;
            case "Units":
                foreach (XElement lUnit in pRoot.Elements(Namespace + "Unit"))
                {
                    pLibrary.Add(ReadUnit(lUnit));
                }

                break;
            default:
                throw Error(pRoot, $"Unexpected element '{pRoot.Name.LocalName}'.");
        }
    }

    private static void ReadDataTypes(ArchetypeLibrary pLibrary, XElement pElement)
    {
        foreach (XElement lType in pElement.Elements())
        {
            if (lType.Name == Namespace + "Description")
            {
                continue;
            }

            string lName = Required(lType, "name");
            string? lSemantics = Text(lType, "Semantics");
            DataTypeDefinition lDefinition = lType.Name.LocalName switch
            {
                "BasicData" => new BasicDataType(
                    lName,
                    lSemantics,
                    int.Parse(Required(lType, "size"), CultureInfo.InvariantCulture),
                    Optional(lType, "interpretation"),
                    Required(lType, "endian"),
                    Optional(lType, "encoding")),
                "SimpleData" => new SimpleDataType(lName, lSemantics, Required(lType, "representation"), Optional(lType, "units")),
                "EnumeratedData" => new EnumeratedDataType(
                    lName,
                    lSemantics,
                    Required(lType, "representation"),
                    lType.Elements(Namespace + "Enumerator")
                        .Select(pEnumerator => new Enumerator(
                            Required(pEnumerator, "name"),
                            long.Parse(Required(pEnumerator, "value"), CultureInfo.InvariantCulture),
                            Text(pEnumerator, "Semantics")))
                        .ToList()),
                "ArrayData" => new ArrayDataType(lName, lSemantics, Required(lType, "dataType"), Required(lType, "cardinality"), Required(lType, "encoding")),
                "FixedRecordData" => new FixedRecordDataType(
                    lName,
                    lSemantics,
                    lType.Elements(Namespace + "Field")
                        .Select(pField => new RecordField(Required(pField, "name"), Required(pField, "dataType"), Text(pField, "Semantics")))
                        .ToList()),
                "VariantRecordData" => new VariantRecordDataType(
                    lName,
                    lSemantics,
                    Required(lType, "discriminant"),
                    Required(lType, "dataType"),
                    lType.Elements(Namespace + "Alternative")
                        .Select(pAlternative => new VariantAlternative(
                            Required(pAlternative, "enumerator"),
                            Optional(pAlternative, "name"),
                            Optional(pAlternative, "dataType")))
                        .ToList()),
                "ReferenceData" => new ReferenceDataType(lName, lSemantics, ParseEnum<ReferenceTarget>(lType, Required(lType, "target"))),
                _ => throw Error(lType, $"Unexpected data type element '{lType.Name.LocalName}'."),
            };
            pLibrary.Add(lDefinition);
        }
    }

    private static void ReadEquipmentLibrary(ArchetypeLibrary pLibrary, XElement pElement)
    {
        foreach (XElement lDefinition in pElement.Elements())
        {
            bool lIsKinematic = lDefinition.Name == Namespace + "KinematicDefinition";
            if (!lIsKinematic && lDefinition.Name != Namespace + "EquipmentDefinition")
            {
                continue;
            }

            pLibrary.Add(new EquipmentDefinition(
                Required(lDefinition, "name"),
                Optional(lDefinition, "base"),
                lIsKinematic,
                Optional(lDefinition, "type"),
                Text(lDefinition, "Description"),
                ReadItems(lDefinition.Element(Namespace + "TechnologicalData")),
                ReadItems(lDefinition.Element(Namespace + "InitializationData")),
                ReadTaskRefs(lDefinition.Element(Namespace + "Tasks"))));
        }
    }

    private static EntityArchetype ReadEntity(XElement pElement)
    {
        XElement lKinematic = pElement.Element(Namespace + "Kinematic") ?? throw Error(pElement, "The entity has no Kinematic.");
        string? lEnvironment = Optional(pElement, "environment");
        return new EntityArchetype(
            Required(pElement, "name"),
            Required(pElement, "disType"),
            lEnvironment is null ? null : ParseEnum<EntityEnvironment>(pElement, lEnvironment),
            Optional(pElement, "folder"),
            Text(pElement, "Description"),
            ReadEquipment(lKinematic, ArchetypeLibrary.RootKinematic, Required(lKinematic, "definition")),
            ReadItems(pElement.Element(Namespace + "Parameters")),
            pElement.Element(Namespace + "Equipments")?.Elements(Namespace + "Equipment")
                .Select(pEquipment => ReadEquipment(pEquipment, Required(pEquipment, "name"), Optional(pEquipment, "definition")))
                .ToList() ?? new List<EquipmentArchetype>(),
            ReadTaskRefs(pElement.Element(Namespace + "Tasks")));
    }

    private static EquipmentArchetype ReadEquipment(XElement pElement, string pName, string? pDefinition)
    {
        return new EquipmentArchetype(
            pName,
            pDefinition,
            Optional(pElement, "type"),
            Text(pElement, "Description"),
            ReadItems(pElement.Element(Namespace + "TechnologicalData")),
            ReadItems(pElement.Element(Namespace + "InitializationData")),
            ReadTaskRefs(pElement.Element(Namespace + "Tasks")));
    }

    private static UnitArchetype ReadUnit(XElement pElement)
    {
        IEnumerable<UnitMember> lEntities = pElement.Elements(Namespace + "EntityRef")
            .Select(pRef => new UnitMember(Required(pRef, "entity"), false, Count(pRef), Optional(pRef, "role")));
        IEnumerable<UnitMember> lUnits = pElement.Elements(Namespace + "UnitRef")
            .Select(pRef => new UnitMember(Required(pRef, "unit"), true, Count(pRef), Optional(pRef, "role")));
        return new UnitArchetype(
            Required(pElement, "name"),
            Optional(pElement, "folder"),
            Text(pElement, "Description"),
            lEntities.Concat(lUnits).ToList(),
            ReadTaskRefs(pElement.Element(Namespace + "Tasks")));
    }

    private static TaskDefinition ReadTask(XElement pElement)
    {
        List<GroupedTaskParameter> lParameters = new List<GroupedTaskParameter>();
        foreach (TaskParameterGroup lGroup in Enum.GetValues<TaskParameterGroup>())
        {
            XElement? lGroupElement = pElement.Element(Namespace + lGroup.ToString());
            if (lGroupElement is null)
            {
                continue;
            }

            foreach (XElement lParameter in lGroupElement.Elements(Namespace + "Parameter"))
            {
                DataItem lItem = ReadItem(lParameter);
                string? lRequired = Optional(lParameter, "required");
                string? lDirection = Optional(lParameter, "direction");
                lParameters.Add(new GroupedTaskParameter(lGroup, new TaskParameter(
                    lItem.Name,
                    lItem.DataType,
                    lItem.Description,
                    lItem.DefaultValue,
                    lItem.Constraints,
                    lRequired is not null && XmlConvert.ToBoolean(lRequired),
                    lDirection is null ? ParameterDirection.In : ParseEnum<ParameterDirection>(lParameter, lDirection))));
            }
        }

        XElement lActivation = pElement.Element(Namespace + "ActivationTrigger") ?? throw Error(pElement, "The task has no ActivationTrigger.");
        List<ExitTrigger> lExits = pElement.Element(Namespace + "ExitTriggers")?.Elements(Namespace + "ExitTrigger")
            .Select(pExit => new ExitTrigger(Optional(pExit, "outcome") ?? "Success", ReadTrigger(pExit)))
            .ToList() ?? new List<ExitTrigger>();
        if (lExits.Count == 0)
        {
            throw Error(pElement, "The task has no exit trigger.");
        }

        return new TaskDefinition(Required(pElement, "name"), Text(pElement, "Description"), lParameters, ReadTrigger(lActivation), lExits);
    }

    private static TriggerDefinition ReadTrigger(XElement pContainer)
    {
        XElement lTrigger = pContainer.Elements().FirstOrDefault(pElement => pElement.Name.Namespace == Namespace)
            ?? throw Error(pContainer, "The trigger is empty.");
        return lTrigger.Name.LocalName switch
        {
            "Immediate" => new ImmediateTrigger(),
            "AtTime" => new AtTimeTrigger(XmlConvert.ToTimeSpan(Required(lTrigger, "delay"))),
            "OnTaskEnd" => new OnTaskEndTrigger(Required(lTrigger, "task"), Optional(lTrigger, "outcome")),
            "OnPositionReached" => new OnPositionReachedTrigger(
                Optional(lTrigger, "parameter"),
                Optional(lTrigger, "tolerance") is { } lTolerance ? XmlConvert.ToDouble(lTolerance) : 0),
            "OnEvent" => new OnEventTrigger(Required(lTrigger, "event")),
            "OnCondition" => new OnConditionTrigger(Required(lTrigger, "expression")),
            "CustomTrigger" => new CustomTrigger(Required(lTrigger, "kind"), ReadArguments(lTrigger)),
            _ => throw Error(lTrigger, $"Unexpected trigger '{lTrigger.Name.LocalName}'."),
        };
    }

    private static List<TaskRef> ReadTaskRefs(XElement? pTasks)
    {
        return pTasks?.Elements(Namespace + "TaskRef")
            .Select(pRef => new TaskRef(Required(pRef, "task"), Text(pRef, "Description"), pRef.Elements(Namespace + "Parameter").Select(ReadItem).ToList()))
            .ToList() ?? new List<TaskRef>();
    }

    private static List<DataItem> ReadItems(XElement? pContainer)
    {
        return pContainer?.Elements(Namespace + "Parameter").Select(ReadItem).ToList() ?? new List<DataItem>();
    }

    private static DataItem ReadItem(XElement pElement)
    {
        List<DataConstraint> lConstraints = new List<DataConstraint>();
        foreach (XElement lConstraint in pElement.Element(Namespace + "Constraints")?.Elements() ?? Enumerable.Empty<XElement>())
        {
            lConstraints.Add(lConstraint.Name.LocalName switch
            {
                "Min" or "Max" => new BoundConstraint(
                    lConstraint.Name.LocalName == "Min" ? BoundKind.Min : BoundKind.Max,
                    Optional(lConstraint, "value"),
                    Optional(lConstraint, "ref"),
                    Optional(lConstraint, "inclusive") is not { } lInclusive || XmlConvert.ToBoolean(lInclusive)),
                "ListOfValues" => new ListOfValuesConstraint(lConstraint.Elements(Namespace + "Value").Select(pValue => pValue.Value.Trim()).ToList()),
                "Pattern" => new PatternConstraint(Required(lConstraint, "value")),
                "CustomConstraint" => new CustomConstraint(Required(lConstraint, "kind"), ReadArguments(lConstraint)),
                _ => throw Error(lConstraint, $"Unexpected constraint '{lConstraint.Name.LocalName}'."),
            });
        }

        return new DataItem(
            Required(pElement, "name"),
            Required(pElement, "dataType"),
            Text(pElement, "Description"),
            pElement.Element(Namespace + "DefaultValue") is { } lDefault ? ReadValue(lDefault) : null,
            lConstraints);
    }

    private static ValueNode ReadValue(XElement pElement)
    {
        string lText = string.Concat(pElement.Nodes().OfType<XText>().Select(pText => pText.Value)).Trim();
        List<KeyValuePair<string, ValueNode>> lFields = pElement.Elements(Namespace + "Field")
            .Select(pField => new KeyValuePair<string, ValueNode>(Required(pField, "name"), ReadValue(pField)))
            .ToList();
        List<ValueNode> lItems = pElement.Elements(Namespace + "Item").Select(ReadValue).ToList();
        return new ValueNode(lText, lFields, lItems);
    }

    private static List<Argument> ReadArguments(XElement pElement)
    {
        return pElement.Elements(Namespace + "Argument").Select(pArgument => new Argument(Required(pArgument, "name"), Required(pArgument, "value"))).ToList();
    }

    private static int Count(XElement pElement)
    {
        int lCount = Optional(pElement, "count") is { } lText ? int.Parse(lText, CultureInfo.InvariantCulture) : 1;
        return lCount > 0 ? lCount : throw Error(pElement, "count must be positive.");
    }

    private static T ParseEnum<T>(XElement pElement, string pText)
        where T : struct, Enum
    {
        return Enum.TryParse(pText, false, out T lValue) && Enum.IsDefined(lValue)
            ? lValue
            : throw Error(pElement, $"'{pText}' is not a valid {typeof(T).Name}.");
    }

    private static string? Text(XElement pElement, string pChild) => pElement.Element(Namespace + pChild)?.Value.Trim();

    private static string? Optional(XElement pElement, string pAttribute) => (string?)pElement.Attribute(pAttribute);

    private static string Required(XElement pElement, string pAttribute)
    {
        return Optional(pElement, pAttribute) ?? throw Error(pElement, $"Attribute '{pAttribute}' is missing.");
    }

    private static XcgfDataException Error(XElement pElement, string pMessage)
    {
        IXmlLineInfo lInfo = pElement;
        string lLocation = lInfo.HasLineInfo() ? $" (line {lInfo.LineNumber})" : string.Empty;
        return new XcgfDataException($"<{pElement.Name.LocalName}>{lLocation}: {pMessage}");
    }
}
