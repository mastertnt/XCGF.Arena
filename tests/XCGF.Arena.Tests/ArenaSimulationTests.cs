using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace XCGF.Arena.Tests;

public class ArenaSimulationTests
{
    [Fact]
    public void Run_StepsUntilDurationAndShortensLastStep()
    {
        ArenaSimulation lSimulation = new ArenaSimulation(TestLibrary.Example);
        RecordingSystem lRecorder = new RecordingSystem();
        lSimulation.Systems.Add(lRecorder);

        lSimulation.Run(TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(2.5), lSimulation.Elapsed);
        Assert.Equal(new[] { (1f, 1f), (1f, 2f), (0.5f, 2.5f) }, lRecorder.Ticks);
        Assert.Throws<ArgumentOutOfRangeException>(() => lSimulation.Step(TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_RunsTasksThenKinematicsThenSensors()
    {
        ArenaSimulation lSimulation = new ArenaSimulation(TestLibrary.Example);

        Assert.Equal(new BaseSystem[] { lSimulation.Tasks, lSimulation.Kinematics, lSimulation.Sensors }, lSimulation.Systems.ChildSystems);
        Assert.Same(lSimulation.Store, lSimulation.Systems.Stores.Single());
    }

    private sealed class RecordingSystem : QuerySystem
    {
        public List<(float Delta, float Time)> Ticks { get; } = new List<(float Delta, float Time)>();

        protected override void OnUpdate() => Ticks.Add((Tick.deltaTime, Tick.time));
    }
}
