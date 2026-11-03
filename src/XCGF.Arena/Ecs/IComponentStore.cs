namespace XCGF.Arena.Ecs;

/// <summary>
/// Untyped view of a component store, used by the world to clean up a destroyed entity.
/// </summary>
internal interface IComponentStore
{
    /// <summary>
    /// Gets the number of components in the store.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Tells whether the entity with the given id has a component in this store.
    /// </summary>
    /// <param name="pEntityId">The entity id.</param>
    /// <returns>True if the entity has a component in this store.</returns>
    bool Contains(int pEntityId);

    /// <summary>
    /// Removes the component of the entity with the given id, if any.
    /// </summary>
    /// <param name="pEntityId">The entity id.</param>
    /// <returns>True if a component was removed.</returns>
    bool Remove(int pEntityId);

    /// <summary>
    /// Copies the entities that have a component in this store.
    /// </summary>
    /// <returns>The entities, in storage order.</returns>
    Entity[] SnapshotEntities();
}
