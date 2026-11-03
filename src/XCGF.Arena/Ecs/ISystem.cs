namespace XCGF.Arena.Ecs;

/// <summary>
/// Behaviour applied to the entities of a world at each simulation step.
/// </summary>
public interface ISystem
{
    /// <summary>
    /// Advances the entities the system handles by one step.
    /// </summary>
    /// <param name="pWorld">The world.</param>
    /// <param name="pTime">The time of the step.</param>
    void Update(World pWorld, SimulationTime pTime);
}
