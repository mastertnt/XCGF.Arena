namespace XCGF.Arena.Data;

/// <summary>
/// Value as written in a library, before it is typed: text for simple and enumerated types, <c>Field</c> children
/// for records, <c>Item</c> children for arrays. Fields and items are values themselves, so records and arrays nest.
/// </summary>
/// <param name="Text">Text of the value, trimmed; empty for a record or an array.</param>
/// <param name="Fields">Field children, by name, in order.</param>
/// <param name="Items">Item children, in order.</param>
public sealed record ValueNode(string Text, IReadOnlyList<KeyValuePair<string, ValueNode>> Fields, IReadOnlyList<ValueNode> Items)
{
    /// <summary>
    /// Gets a value indicating whether the value is plain text, without fields or items.
    /// </summary>
    public bool IsText => Fields.Count == 0 && Items.Count == 0;

    /// <summary>
    /// Builds a plain text value.
    /// </summary>
    /// <param name="pText">The text.</param>
    /// <returns>The value.</returns>
    public static ValueNode Of(string pText)
    {
        ArgumentNullException.ThrowIfNull(pText);
        return new ValueNode(pText.Trim(), Array.Empty<KeyValuePair<string, ValueNode>>(), Array.Empty<ValueNode>());
    }

    /// <summary>
    /// Builds a plain text value.
    /// </summary>
    /// <param name="pText">The text.</param>
    public static implicit operator ValueNode(string pText) => Of(pText);

    /// <inheritdoc/>
    public bool Equals(ValueNode? pOther)
    {
        return pOther is not null
            && Text == pOther.Text
            && Fields.SequenceEqual(pOther.Fields)
            && Items.SequenceEqual(pOther.Items);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Text, Fields.Count, Items.Count);

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsText)
        {
            return Text;
        }

        IEnumerable<string> lParts = Fields.Select(pField => $"{pField.Key}={pField.Value}").Concat(Items.Select(pItem => pItem.ToString()));
        return (Fields.Count > 0 ? "{" : "[") + string.Join(", ", lParts) + (Fields.Count > 0 ? "}" : "]");
    }
}
