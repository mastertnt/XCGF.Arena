namespace XCGF.Arena.Ecs;

/// <summary>
/// Handle of an entity in a <see cref="World"/>. An entity is only an identifier; its data lives in its components.
/// </summary>
/// <param name="Id">Index of the entity in its world. Ids are recycled once an entity is destroyed.</param>
/// <param name="Generation">Generation of the id, incremented each time the id is recycled, so a stale handle is never mistaken for a new entity.</param>
public readonly record struct Entity(int Id, int Generation)
{
    /// <summary>
    /// Handle that designates no entity.
    /// </summary>
    public static readonly Entity Null = new Entity(-1, 0);

    /// <summary>
    /// Gets a value indicating whether this handle is <see cref="Null"/>.
    /// </summary>
    public bool IsNull => Id < 0;

    /// <inheritdoc/>
    public override string ToString() => IsNull ? "Entity(null)" : $"Entity({Id}:{Generation})";
}
