namespace XCGF.Arena.Data;

/// <summary>
/// Name and value of an argument passed to a custom constraint or trigger.
/// </summary>
/// <param name="Name">Name of the argument.</param>
/// <param name="Value">Value of the argument.</param>
public sealed record Argument(string Name, string Value);

/// <summary>
/// Constraint on the value of a data item.
/// </summary>
public abstract record DataConstraint;

/// <summary>
/// Kind of bound.
/// </summary>
public enum BoundKind
{
    /// <summary>Lower bound.</summary>
    Min,

    /// <summary>Upper bound.</summary>
    Max,
}

/// <summary>
/// Lower or upper bound. The bound is either a literal value or a reference to a technological data item.
/// </summary>
/// <param name="Kind">Whether it is a lower or an upper bound.</param>
/// <param name="Value">Literal bound, if any.</param>
/// <param name="Reference">Name of the technological data item holding the bound, if any.</param>
/// <param name="Inclusive">Whether the bound itself is allowed.</param>
public sealed record BoundConstraint(BoundKind Kind, string? Value, string? Reference, bool Inclusive) : DataConstraint;

/// <summary>
/// Closed list of allowed values.
/// </summary>
/// <param name="Values">The allowed values.</param>
public sealed record ListOfValuesConstraint(IReadOnlyList<string> Values) : DataConstraint;

/// <summary>
/// Regular expression the textual value must match.
/// </summary>
/// <param name="Pattern">The regular expression.</param>
public sealed record PatternConstraint(string Pattern) : DataConstraint;

/// <summary>
/// Constraint checked by a tool that knows its kind.
/// </summary>
/// <param name="Kind">Kind of the constraint.</param>
/// <param name="Arguments">Arguments of the constraint.</param>
public sealed record CustomConstraint(string Kind, IReadOnlyList<Argument> Arguments) : DataConstraint;
