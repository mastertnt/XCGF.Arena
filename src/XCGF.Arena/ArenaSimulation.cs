using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Data;
using XCGF.Arena.Spawning;
using XCGF.Arena.Systems;

namespace XCGF.Arena;

/// <summary>
/// Wires an XCGF.Data library to a Friflo <see cref="EntityStore"/>: the factory that spawns the archetypes, the
/// systems that run them, and the simulation clock.
/// </summary>
public sealed class ArenaSimulation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArenaSimulation"/> class.
    /// </summary>
    /// <param name="pLibrary">The archetypes.</param>
    public ArenaSimulation(ArchetypeLibrary pLibrary)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        Library = pLibrary;
        Store = new EntityStore();
        Factory = new ArchetypeFactory(Store, pLibrary);
        Tasks = new TaskSystem(pLibrary);
        Kinematics = new KinematicSystem();
        Sensors = new SensorSystem();
        Systems = new SystemRoot(Store, "Arena");
        Systems.Add(Tasks);
        Systems.Add(Kinematics);
        Systems.Add(Sensors);
    }

    /// <summary>
    /// Gets the archetypes.
    /// </summary>
    public ArchetypeLibrary Library { get; }

    /// <summary>
    /// Gets the store holding the entities.
    /// </summary>
    public EntityStore Store { get; }

    /// <summary>
    /// Gets the factory that spawns the archetypes into the store.
    /// </summary>
    public ArchetypeFactory Factory { get; }

    /// <summary>
    /// Gets the task system, used to assign tasks and raise events.
    /// </summary>
    public TaskSystem Tasks { get; }

    /// <summary>
    /// Gets the kinematic system.
    /// </summary>
    public KinematicSystem Kinematics { get; }

    /// <summary>
    /// Gets the sensor system, used to register the detection models of the sensor types.
    /// </summary>
    public SensorSystem Sensors { get; }

    /// <summary>
    /// Gets the systems, run in order at each step: tasks, kinematics, then sensors, so that sensors see the positions
    /// reached during the step. More systems can be added.
    /// </summary>
    public SystemRoot Systems { get; }

    /// <summary>
    /// Gets the simulation time elapsed since the first step.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

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
        Systems.Update(new UpdateTick((float)pDelta.TotalSeconds, (float)Elapsed.TotalSeconds));
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
