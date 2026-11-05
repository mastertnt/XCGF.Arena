namespace XCGF.Arena.Dis;

/// <summary>
/// Destination of the entity states the <see cref="DisSystem"/> produces: a network sender, a recorder...
/// </summary>
public interface IDisPublisher
{
    /// <summary>
    /// Publishes the state of one entity.
    /// </summary>
    /// <param name="pState">The state.</param>
    void Publish(DisEntityState pState);
}
