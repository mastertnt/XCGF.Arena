using XCGF.Arena.Data;

namespace XCGF.Arena.Components;

/// <summary>
/// Values of a group of data items (technological data, initialization data or parameters), keyed by name.
/// </summary>
public sealed class ParameterValues
{
    private readonly Dictionary<string, DataItem> mItems = new Dictionary<string, DataItem>();
    private readonly Dictionary<string, DataValue?> mValues = new Dictionary<string, DataValue?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterValues"/> class.
    /// </summary>
    /// <param name="pItems">The data items. Their values start unset.</param>
    public ParameterValues(IEnumerable<DataItem> pItems)
    {
        ArgumentNullException.ThrowIfNull(pItems);
        foreach (DataItem lItem in pItems)
        {
            mItems[lItem.Name] = lItem;
            mValues[lItem.Name] = null;
        }
    }

    /// <summary>
    /// Gets the data items, by name.
    /// </summary>
    public IReadOnlyDictionary<string, DataItem> Items => mItems;

    /// <summary>
    /// Gets the values, by name. A null value is unset.
    /// </summary>
    public IReadOnlyDictionary<string, DataValue?> Values => mValues;

    /// <summary>
    /// Tells whether a data item is declared.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>True if declared.</returns>
    public bool Contains(string pName) => mItems.ContainsKey(pName);

    /// <summary>
    /// Gets the value of a data item.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The value, or null when it is unset or not declared.</returns>
    public DataValue? Get(string pName) => mValues.TryGetValue(pName, out DataValue? lValue) ? lValue : null;

    /// <summary>
    /// Gets a numeric value.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <returns>The number, or null when it is unset, not declared or not numeric.</returns>
    public double? GetNumber(string pName) => Get(pName)?.AsNumber();

    /// <summary>
    /// Sets the value of a declared data item. Constraints are not checked here.
    /// </summary>
    /// <param name="pName">The name.</param>
    /// <param name="pValue">The value, or null to unset it.</param>
    public void Set(string pName, DataValue? pValue)
    {
        if (!mItems.ContainsKey(pName))
        {
            throw new XcgfDataException($"'{pName}' is not declared.");
        }

        mValues[pName] = pValue;
    }
}
