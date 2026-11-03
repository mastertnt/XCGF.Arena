namespace XCGF.Arena.Ecs;

/// <summary>
/// Runs systems on a world, in the order they were added, one step after the other.
/// </summary>
public sealed class Simulation
{
    private readonly List<ISystem> mSystems = new List<ISystem>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Simulation"/> class.
    /// </summary>
    /// <param name="pWorld">The world the systems run on.</param>
    public Simulation(World pWorld)
    {
        ArgumentNullException.ThrowIfNull(pWorld);
        World = pWorld;
    }

    /// <summary>
    /// Gets the world the systems run on.
    /// </summary>
    public World World { get; }

    /// <summary>
    /// Gets the simulation time elapsed since the first step.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// Gets the systems, in execution order.
    /// </summary>
    public IReadOnlyList<ISystem> Systems => mSystems;

    /// <summary>
    /// Adds a system after the existing ones.
    /// </summary>
    /// <param name="pSystem">The system.</param>
    /// <returns>This simulation, to chain calls.</returns>
    public Simulation AddSystem(ISystem pSystem)
    {
        ArgumentNullException.ThrowIfNull(pSystem);
        mSystems.Add(pSystem);
        return this;
    }

    /// <summary>
    /// Runs every system once.
    /// </summary>
    /// <param name="pDelta">The duration of the step. It must be positive.</param>
    public void Step(TimeSpan pDelta)
    {
        if (pDelta <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pDelta), pDelta, "The step duration must be positive.");
        }

        Elapsed += pDelta;
        SimulationTime lTime = new SimulationTime(Elapsed, pDelta);
        foreach (ISystem lSystem in mSystems)
        {
            lSystem.Update(World, lTime);
        }
    }

    /// <summary>
    /// Runs steps of the given duration until the given time has elapsed.
    /// </summary>
    /// <param name="pDuration">The total duration to simulate.</param>
    /// <param name="pDelta">The duration of each step. The last step is shortened to end exactly on time.</param>
    public void Run(TimeSpan pDuration, TimeSpan pDelta)
    {
        TimeSpan lEnd = Elapsed + pDuration;
        while (Elapsed < lEnd)
        {
            TimeSpan lRemaining = lEnd - Elapsed;
            Step(lRemaining < pDelta ? lRemaining : pDelta);
        }
    }
}
