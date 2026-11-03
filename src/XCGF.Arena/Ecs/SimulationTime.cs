namespace XCGF.Arena.Ecs;

/// <summary>
/// Time of a simulation step.
/// </summary>
/// <param name="Elapsed">Simulation time at the end of the step, since the simulation started.</param>
/// <param name="Delta">Duration of the step.</param>
public readonly record struct SimulationTime(TimeSpan Elapsed, TimeSpan Delta);
