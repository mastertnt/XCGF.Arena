namespace XCGF.Arena.Ecs;

/// <summary>
/// Sparse set that stores the components of one type, densely packed so that iteration is cache friendly.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
internal sealed class ComponentStore<T> : IComponentStore
{
    private const int NoIndex = -1;
    private const int InitialCapacity = 16;

    private int[] mSparse = Array.Empty<int>();
    private Entity[] mEntities = new Entity[InitialCapacity];
    private T[] mValues = new T[InitialCapacity];
    private int mCount;

    /// <inheritdoc/>
    public int Count => mCount;

    /// <inheritdoc/>
    public bool Contains(int pEntityId) => IndexOf(pEntityId) != NoIndex;

    /// <summary>
    /// Adds or replaces the component of an entity.
    /// </summary>
    /// <param name="pEntity">The entity.</param>
    /// <param name="pValue">The component.</param>
    public void Set(Entity pEntity, T pValue)
    {
        int lIndex = IndexOf(pEntity.Id);
        if (lIndex != NoIndex)
        {
            mValues[lIndex] = pValue;
            return;
        }

        EnsureSparseCapacity(pEntity.Id);
        if (mCount == mValues.Length)
        {
            Array.Resize(ref mEntities, mCount * 2);
            Array.Resize(ref mValues, mCount * 2);
        }

        mEntities[mCount] = pEntity;
        mValues[mCount] = pValue;
        mSparse[pEntity.Id] = mCount;
        mCount++;
    }

    /// <summary>
    /// Gets a reference to the component of an entity, so that a struct component can be modified in place.
    /// </summary>
    /// <param name="pEntityId">The entity id.</param>
    /// <returns>A reference to the component.</returns>
    /// <exception cref="KeyNotFoundException">The entity has no such component.</exception>
    public ref T Get(int pEntityId)
    {
        int lIndex = IndexOf(pEntityId);
        if (lIndex == NoIndex)
        {
            throw new KeyNotFoundException($"Entity {pEntityId} has no {typeof(T).Name} component.");
        }

        return ref mValues[lIndex];
    }

    /// <summary>
    /// Gets the component of an entity, if any.
    /// </summary>
    /// <param name="pEntityId">The entity id.</param>
    /// <param name="pValue">The component, or the default value.</param>
    /// <returns>True if the entity has the component.</returns>
    public bool TryGet(int pEntityId, out T pValue)
    {
        int lIndex = IndexOf(pEntityId);
        if (lIndex == NoIndex)
        {
            pValue = default!;
            return false;
        }

        pValue = mValues[lIndex];
        return true;
    }

    /// <inheritdoc/>
    public bool Remove(int pEntityId)
    {
        int lIndex = IndexOf(pEntityId);
        if (lIndex == NoIndex)
        {
            return false;
        }

        // Move the last component into the hole to keep the storage dense.
        int lLast = mCount - 1;
        if (lIndex != lLast)
        {
            mEntities[lIndex] = mEntities[lLast];
            mValues[lIndex] = mValues[lLast];
            mSparse[mEntities[lIndex].Id] = lIndex;
        }

        mEntities[lLast] = default;
        mValues[lLast] = default!;
        mSparse[pEntityId] = NoIndex;
        mCount--;
        return true;
    }

    /// <inheritdoc/>
    public Entity[] SnapshotEntities() => mEntities.AsSpan(0, mCount).ToArray();

    private int IndexOf(int pEntityId)
    {
        return pEntityId >= 0 && pEntityId < mSparse.Length ? mSparse[pEntityId] : NoIndex;
    }

    private void EnsureSparseCapacity(int pEntityId)
    {
        if (pEntityId < mSparse.Length)
        {
            return;
        }

        int lOldLength = mSparse.Length;
        int lNewLength = Math.Max(Math.Max(InitialCapacity, lOldLength * 2), pEntityId + 1);
        Array.Resize(ref mSparse, lNewLength);
        Array.Fill(mSparse, NoIndex, lOldLength, lNewLength - lOldLength);
    }
}
