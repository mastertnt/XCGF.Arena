namespace XCGF.Arena.Ecs;

/// <summary>
/// Container of the entities and of their components.
/// </summary>
/// <remarks>
/// An entity holds at most one component of each type. Queries return a snapshot, so systems can add or remove
/// components and entities while they iterate.
/// </remarks>
public sealed class World
{
    private readonly List<int> mGenerations = new List<int>();
    private readonly List<bool> mAlive = new List<bool>();
    private readonly Stack<int> mFreeIds = new Stack<int>();
    private readonly Dictionary<Type, IComponentStore> mStores = new Dictionary<Type, IComponentStore>();

    /// <summary>
    /// Gets the number of living entities.
    /// </summary>
    public int EntityCount { get; private set; }

    /// <summary>
    /// Creates an entity without components.
    /// </summary>
    /// <returns>The new entity.</returns>
    public Entity CreateEntity()
    {
        int lId;
        if (mFreeIds.Count > 0)
        {
            lId = mFreeIds.Pop();
            mGenerations[lId]++;
            mAlive[lId] = true;
        }
        else
        {
            lId = mGenerations.Count;
            mGenerations.Add(0);
            mAlive.Add(true);
        }

        EntityCount++;
        return new Entity(lId, mGenerations[lId]);
    }

    /// <summary>
    /// Tells whether a handle designates a living entity of this world.
    /// </summary>
    /// <param name="pEntity">The entity.</param>
    /// <returns>True if the entity is alive.</returns>
    public bool IsAlive(Entity pEntity)
    {
        return pEntity.Id >= 0
            && pEntity.Id < mGenerations.Count
            && mAlive[pEntity.Id]
            && mGenerations[pEntity.Id] == pEntity.Generation;
    }

    /// <summary>
    /// Destroys an entity and all its components. Destroying a dead entity does nothing.
    /// </summary>
    /// <param name="pEntity">The entity.</param>
    /// <returns>True if the entity was alive.</returns>
    public bool DestroyEntity(Entity pEntity)
    {
        if (!IsAlive(pEntity))
        {
            return false;
        }

        foreach (IComponentStore lStore in mStores.Values)
        {
            lStore.Remove(pEntity.Id);
        }

        mAlive[pEntity.Id] = false;
        mFreeIds.Push(pEntity.Id);
        EntityCount--;
        return true;
    }

    /// <summary>
    /// Adds a component to an entity, or replaces the one it already has.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="pEntity">The entity.</param>
    /// <param name="pComponent">The component.</param>
    public void Set<T>(Entity pEntity, T pComponent)
    {
        EnsureAlive(pEntity);
        GetOrCreateStore<T>().Set(pEntity, pComponent);
    }

    /// <summary>
    /// Tells whether an entity has a component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="pEntity">The entity.</param>
    /// <returns>True if the entity is alive and has the component.</returns>
    public bool Has<T>(Entity pEntity)
    {
        return IsAlive(pEntity) && TryGetStore<T>() is { } lStore && lStore.Contains(pEntity.Id);
    }

    /// <summary>
    /// Gets a reference to a component of an entity, so that a struct component can be modified in place.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="pEntity">The entity.</param>
    /// <returns>A reference to the component. It is valid until a component of the same type is added or removed.</returns>
    /// <exception cref="InvalidOperationException">The entity is not alive.</exception>
    /// <exception cref="KeyNotFoundException">The entity has no such component.</exception>
    public ref T Get<T>(Entity pEntity)
    {
        EnsureAlive(pEntity);
        ComponentStore<T>? lStore = TryGetStore<T>();
        if (lStore is null)
        {
            throw new KeyNotFoundException($"{pEntity} has no {typeof(T).Name} component.");
        }

        return ref lStore.Get(pEntity.Id);
    }

    /// <summary>
    /// Gets a component of an entity, if any.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="pEntity">The entity.</param>
    /// <param name="pComponent">The component, or the default value.</param>
    /// <returns>True if the entity is alive and has the component.</returns>
    public bool TryGet<T>(Entity pEntity, out T pComponent)
    {
        if (IsAlive(pEntity) && TryGetStore<T>() is { } lStore)
        {
            return lStore.TryGet(pEntity.Id, out pComponent);
        }

        pComponent = default!;
        return false;
    }

    /// <summary>
    /// Removes a component from an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="pEntity">The entity.</param>
    /// <returns>True if the component was removed.</returns>
    public bool Remove<T>(Entity pEntity)
    {
        return IsAlive(pEntity) && TryGetStore<T>() is { } lStore && lStore.Remove(pEntity.Id);
    }

    /// <summary>
    /// Gets the entities that have a component.
    /// </summary>
    /// <typeparam name="T1">The component type.</typeparam>
    /// <returns>A snapshot of the matching entities.</returns>
    public IReadOnlyList<Entity> Query<T1>()
    {
        return Query(typeof(T1));
    }

    /// <summary>
    /// Gets the entities that have all the given components.
    /// </summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <returns>A snapshot of the matching entities.</returns>
    public IReadOnlyList<Entity> Query<T1, T2>()
    {
        return Query(typeof(T1), typeof(T2));
    }

    /// <summary>
    /// Gets the entities that have all the given components.
    /// </summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <typeparam name="T3">The third component type.</typeparam>
    /// <returns>A snapshot of the matching entities.</returns>
    public IReadOnlyList<Entity> Query<T1, T2, T3>()
    {
        return Query(typeof(T1), typeof(T2), typeof(T3));
    }

    private IReadOnlyList<Entity> Query(params Type[] pTypes)
    {
        List<IComponentStore> lStores = new List<IComponentStore>(pTypes.Length);
        foreach (Type lType in pTypes)
        {
            if (!mStores.TryGetValue(lType, out IComponentStore? lStore))
            {
                return Array.Empty<Entity>();
            }

            lStores.Add(lStore);
        }

        // Iterate the smallest store and filter with the others.
        lStores.Sort((pLeft, pRight) => pLeft.Count.CompareTo(pRight.Count));
        Entity[] lCandidates = lStores[0].SnapshotEntities();
        if (lStores.Count == 1)
        {
            return lCandidates;
        }

        List<Entity> lResult = new List<Entity>(lCandidates.Length);
        foreach (Entity lEntity in lCandidates)
        {
            bool lMatches = true;
            for (int lIndex = 1; lIndex < lStores.Count && lMatches; lIndex++)
            {
                lMatches = lStores[lIndex].Contains(lEntity.Id);
            }

            if (lMatches)
            {
                lResult.Add(lEntity);
            }
        }

        return lResult;
    }

    private ComponentStore<T>? TryGetStore<T>()
    {
        return mStores.TryGetValue(typeof(T), out IComponentStore? lStore) ? (ComponentStore<T>)lStore : null;
    }

    private ComponentStore<T> GetOrCreateStore<T>()
    {
        ComponentStore<T>? lStore = TryGetStore<T>();
        if (lStore is null)
        {
            lStore = new ComponentStore<T>();
            mStores.Add(typeof(T), lStore);
        }

        return lStore;
    }

    private void EnsureAlive(Entity pEntity)
    {
        if (!IsAlive(pEntity))
        {
            throw new InvalidOperationException($"{pEntity} is not alive in this world.");
        }
    }
}
