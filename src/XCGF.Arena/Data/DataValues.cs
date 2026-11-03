using System.Globalization;
using XCGF.Arena.Ecs;

namespace XCGF.Arena.Data;

/// <summary>
/// Value of a data item at run time, typed after its data type.
/// </summary>
public abstract record DataValue
{
    /// <summary>
    /// Gets the value as a number, for the values that have one.
    /// </summary>
    /// <returns>The number, or null when the value is not numeric.</returns>
    public virtual double? AsNumber() => null;
}

/// <summary>
/// Floating point value.
/// </summary>
/// <param name="Value">The number.</param>
public sealed record NumberValue(double Value) : DataValue
{
    /// <inheritdoc/>
    public override double? AsNumber() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Integer value.
/// </summary>
/// <param name="Value">The integer.</param>
public sealed record IntegerValue(long Value) : DataValue
{
    /// <inheritdoc/>
    public override double? AsNumber() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Enumerator of an enumerated type.
/// </summary>
/// <param name="Name">Name of the enumerator.</param>
/// <param name="Value">Numeric value of the enumerator.</param>
public sealed record EnumValue(string Name, long Value) : DataValue
{
    /// <inheritdoc/>
    public override double? AsNumber() => Value;

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>
/// Character string, for arrays of octets.
/// </summary>
/// <param name="Value">The text.</param>
public sealed record TextValue(string Value) : DataValue
{
    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Fixed or variant record.
/// </summary>
/// <param name="Fields">Values of the fields, by name.</param>
public sealed record RecordValue(IReadOnlyDictionary<string, DataValue> Fields) : DataValue
{
    /// <summary>
    /// Gets a numeric field.
    /// </summary>
    /// <param name="pName">The field name.</param>
    /// <returns>The number, or 0 when the field is missing or not numeric.</returns>
    public double GetNumber(string pName)
    {
        return Fields.TryGetValue(pName, out DataValue? lValue) ? lValue.AsNumber() ?? 0 : 0;
    }

    /// <summary>
    /// Builds a record from name and value pairs.
    /// </summary>
    /// <param name="pFields">The fields.</param>
    /// <returns>The record.</returns>
    public static RecordValue Of(params (string Name, DataValue Value)[] pFields)
    {
        Dictionary<string, DataValue> lFields = new Dictionary<string, DataValue>();
        foreach ((string lName, DataValue lValue) in pFields)
        {
            lFields[lName] = lValue;
        }

        return new RecordValue(lFields);
    }

    /// <inheritdoc/>
    public bool Equals(RecordValue? pOther)
    {
        if (pOther is null || pOther.Fields.Count != Fields.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, DataValue> lField in Fields)
        {
            if (!pOther.Fields.TryGetValue(lField.Key, out DataValue? lOther) || !Equals(lField.Value, lOther))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Fields.Count;

    /// <inheritdoc/>
    public override string ToString() => "{" + string.Join(", ", Fields.Select(pField => $"{pField.Key}={pField.Value}")) + "}";
}

/// <summary>
/// Array of values.
/// </summary>
/// <param name="Items">The elements.</param>
public sealed record ArrayValue(IReadOnlyList<DataValue> Items) : DataValue
{
    /// <inheritdoc/>
    public bool Equals(ArrayValue? pOther)
    {
        return pOther is not null && Items.SequenceEqual(pOther.Items);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Items.Count;

    /// <inheritdoc/>
    public override string ToString() => "[" + string.Join(", ", Items) + "]";
}

/// <summary>
/// Reference to an entity, equipment or unit instance of the world.
/// </summary>
/// <param name="Target">The instance.</param>
public sealed record ReferenceValue(Entity Target) : DataValue
{
    /// <inheritdoc/>
    public override string ToString() => Target.ToString();
}
