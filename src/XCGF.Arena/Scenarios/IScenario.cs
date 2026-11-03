namespace XCGF.Arena.Scenarios;

/// <summary>
/// Scenario written in code: it creates the instances and drives them while the simulation runs.
/// </summary>
public interface IScenario
{
    /// <summary>
    /// Gets the name of the scenario.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates the instances and assigns their first tasks, before the first step.
    /// </summary>
    /// <param name="pArena">The simulation.</param>
    void Setup(ArenaSimulation pArena);

    /// <summary>
    /// Called after each step, to script what the scenario does next.
    /// </summary>
    /// <param name="pArena">The simulation.</param>
    void Update(ArenaSimulation pArena);
}
