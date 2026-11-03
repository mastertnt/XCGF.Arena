namespace XCGF.Arena.Data;

/// <summary>
/// Data type of the library, modelled on the HLA OMT (IEEE 1516-2010).
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type, if documented.</param>
public abstract record DataTypeDefinition(string Name, string? Semantics);

/// <summary>
/// Basic data: a raw representation (integer, floating point, byte...).
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="Size">Size in bits.</param>
/// <param name="Interpretation">How the bits are interpreted, e.g. "Signed integer".</param>
/// <param name="Endian">Byte order, Big or Little.</param>
/// <param name="Encoding">Encoding of the bits, e.g. "IEEE 754".</param>
public sealed record BasicDataType(string Name, string? Semantics, int Size, string? Interpretation, string Endian, string? Encoding)
    : DataTypeDefinition(Name, Semantics)
{
    /// <summary>
    /// Gets a value indicating whether the type holds a floating point number rather than an integer.
    /// </summary>
    public bool IsFloatingPoint =>
        (Interpretation?.Contains("float", StringComparison.OrdinalIgnoreCase) ?? false)
        || (Encoding?.Contains("754", StringComparison.Ordinal) ?? false);
}

/// <summary>
/// Simple data: a basic representation with units.
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="Representation">Name of the basic type that represents it.</param>
/// <param name="Units">Units of the value, e.g. "m/s".</param>
public sealed record SimpleDataType(string Name, string? Semantics, string Representation, string? Units)
    : DataTypeDefinition(Name, Semantics);

/// <summary>
/// One value of an enumerated data type.
/// </summary>
/// <param name="Name">Name of the enumerator.</param>
/// <param name="Value">Numeric value of the enumerator.</param>
/// <param name="Semantics">Meaning of the enumerator.</param>
public sealed record Enumerator(string Name, long Value, string? Semantics);

/// <summary>
/// Enumerated data: a closed list of named values.
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="Representation">Name of the basic type that represents it.</param>
/// <param name="Enumerators">The values.</param>
public sealed record EnumeratedDataType(string Name, string? Semantics, string Representation, IReadOnlyList<Enumerator> Enumerators)
    : DataTypeDefinition(Name, Semantics)
{
    /// <summary>
    /// Finds an enumerator by name or by numeric value.
    /// </summary>
    /// <param name="pText">The name or the value.</param>
    /// <returns>The enumerator, or null if none matches.</returns>
    public Enumerator? Find(string pText)
    {
        foreach (Enumerator lEnumerator in Enumerators)
        {
            if (lEnumerator.Name == pText)
            {
                return lEnumerator;
            }
        }

        if (long.TryParse(pText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long lValue))
        {
            foreach (Enumerator lEnumerator in Enumerators)
            {
                if (lEnumerator.Value == lValue)
                {
                    return lEnumerator;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Array data: a sequence of elements of one type.
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="ElementType">Name of the type of the elements.</param>
/// <param name="Cardinality">Number of elements: a count, a range such as "1..100", or "Dynamic".</param>
/// <param name="Encoding">Encoding of the array.</param>
public sealed record ArrayDataType(string Name, string? Semantics, string ElementType, string Cardinality, string Encoding)
    : DataTypeDefinition(Name, Semantics);

/// <summary>
/// Field of a fixed record.
/// </summary>
/// <param name="Name">Name of the field.</param>
/// <param name="DataType">Name of the type of the field.</param>
/// <param name="Semantics">Meaning of the field.</param>
public sealed record RecordField(string Name, string DataType, string? Semantics);

/// <summary>
/// Fixed record data: a structure of named fields.
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="Fields">The fields, in order.</param>
public sealed record FixedRecordDataType(string Name, string? Semantics, IReadOnlyList<RecordField> Fields)
    : DataTypeDefinition(Name, Semantics);

/// <summary>
/// Alternative of a variant record.
/// </summary>
/// <param name="Enumerators">Enumerators of the discriminant that select the alternative.</param>
/// <param name="Name">Name of the alternative, if it carries data.</param>
/// <param name="DataType">Type of the alternative, if it carries data.</param>
public sealed record VariantAlternative(string Enumerators, string? Name, string? DataType);

/// <summary>
/// Variant record data: a discriminant that selects one alternative.
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="Discriminant">Name of the discriminant.</param>
/// <param name="DiscriminantType">Enumerated type of the discriminant.</param>
/// <param name="Alternatives">The alternatives.</param>
public sealed record VariantRecordDataType(string Name, string? Semantics, string Discriminant, string DiscriminantType, IReadOnlyList<VariantAlternative> Alternatives)
    : DataTypeDefinition(Name, Semantics);

/// <summary>
/// Kind of instance a reference data item points to.
/// </summary>
public enum ReferenceTarget
{
    /// <summary>An entity instance.</summary>
    Entity,

    /// <summary>An equipment instance.</summary>
    Equipment,

    /// <summary>A unit instance.</summary>
    Unit,
}

/// <summary>
/// Reference data (XCGF extension): a data item that points to an instance chosen in the scenario.
/// </summary>
/// <param name="Name">Unique name of the type.</param>
/// <param name="Semantics">Meaning of the type.</param>
/// <param name="Target">Kind of instance it points to.</param>
public sealed record ReferenceDataType(string Name, string? Semantics, ReferenceTarget Target)
    : DataTypeDefinition(Name, Semantics);
