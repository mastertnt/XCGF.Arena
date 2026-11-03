namespace XCGF.Arena.Data;

/// <summary>
/// Data item declared by an archetype: a technological data, an initialization data or a parameter.
/// </summary>
/// <param name="Name">Name of the item, unique in its group.</param>
/// <param name="DataType">Name of its data type.</param>
/// <param name="Description">What the item means.</param>
/// <param name="DefaultValue">Default value as written in the library, if any.</param>
/// <param name="Constraints">Constraints on its value.</param>
public record DataItem(string Name, string DataType, string? Description, string? DefaultValue, IReadOnlyList<DataConstraint> Constraints)
{
    /// <summary>
    /// Builds the item obtained when another declaration completes or overrides this one.
    /// </summary>
    /// <param name="pOverride">The overriding declaration.</param>
    /// <returns>The merged item: what the override declares wins, the rest is kept.</returns>
    public DataItem MergeWith(DataItem pOverride)
    {
        ArgumentNullException.ThrowIfNull(pOverride);
        if (pOverride.DataType != DataType)
        {
            throw new XcgfDataException(
                $"Data item '{Name}' is redeclared with type '{pOverride.DataType}' instead of '{DataType}'.");
        }

        return this with
        {
            Description = pOverride.Description ?? Description,
            DefaultValue = pOverride.DefaultValue ?? DefaultValue,
            Constraints = pOverride.Constraints.Count > 0 ? pOverride.Constraints : Constraints,
        };
    }
}

/// <summary>
/// Direction of a task parameter.
/// </summary>
public enum ParameterDirection
{
    /// <summary>Supplied to the task.</summary>
    In,

    /// <summary>Set by the task.</summary>
    Out,

    /// <summary>Supplied to the task and updated by it.</summary>
    InOut,
}

/// <summary>
/// Parameter of a task.
/// </summary>
/// <param name="Name">Name of the parameter, unique in the task.</param>
/// <param name="DataType">Name of its data type.</param>
/// <param name="Description">What the parameter means.</param>
/// <param name="DefaultValue">Default value as written in the library, if any.</param>
/// <param name="Constraints">Constraints on its value.</param>
/// <param name="Required">Whether the scenario must supply it.</param>
/// <param name="Direction">Whether it is an input, an output or both.</param>
public sealed record TaskParameter(
    string Name,
    string DataType,
    string? Description,
    string? DefaultValue,
    IReadOnlyList<DataConstraint> Constraints,
    bool Required,
    ParameterDirection Direction)
    : DataItem(Name, DataType, Description, DefaultValue, Constraints);
