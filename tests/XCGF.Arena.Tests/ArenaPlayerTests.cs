using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Scenarios;

namespace XCGF.Arena.Tests;

public class ArenaPlayerTests
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    private readonly ArenaPlayer mPlayer = new ArenaPlayer(TestLibrary.Example, () => new TwoFightersScenario(), sStep);

    [Fact]
    public void New_IsStoppedWithTheScenarioSetUp()
    {
        Assert.Equal(PlaybackState.Stopped, mPlayer.State);
        Assert.Equal(TimeSpan.Zero, mPlayer.Simulation.Elapsed);
        Assert.Equal(2, mPlayer.Simulation.Store.Query<Kinematic, Force>().Count);
    }

    [Fact]
    public void Advance_WhenStopped_RunsNothing()
    {
        Assert.Equal(0, mPlayer.Advance(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.Zero, mPlayer.Simulation.Elapsed);
    }

    [Fact]
    public void Advance_WhenPlaying_RunsTheStepsDueAndKeepsTheRemainder()
    {
        mPlayer.Play();

        Assert.Equal(2, mPlayer.Advance(TimeSpan.FromSeconds(2.5)));
        Assert.Equal(1, mPlayer.Advance(TimeSpan.FromSeconds(0.5)));
        Assert.Equal(TimeSpan.FromSeconds(3), mPlayer.Simulation.Elapsed);
    }

    [Fact]
    public void Advance_ScalesTheWallClockBySpeed()
    {
        mPlayer.Speed = 10;
        mPlayer.Play();

        mPlayer.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(10), mPlayer.Simulation.Elapsed);
    }

    [Fact]
    public void Pause_FreezesTimeAndPlayResumes()
    {
        mPlayer.Play();
        mPlayer.Advance(TimeSpan.FromSeconds(2));
        mPlayer.Pause();

        Assert.Equal(0, mPlayer.Advance(TimeSpan.FromSeconds(5)));
        Assert.Equal(PlaybackState.Paused, mPlayer.State);

        mPlayer.Play();
        mPlayer.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(3), mPlayer.Simulation.Elapsed);
    }

    [Fact]
    public void Pause_WhenStopped_StaysStopped()
    {
        mPlayer.Pause();

        Assert.Equal(PlaybackState.Stopped, mPlayer.State);
    }

    [Fact]
    public void Stop_ResetsTheScenarioToItsStart()
    {
        mPlayer.Play();
        Entity lBlue = ((TwoFightersScenario)mPlayer.Scenario).Blue;
        GeoPosition lStart = lBlue.GetComponent<Kinematic>().Position;
        mPlayer.Advance(TimeSpan.FromSeconds(60));
        Assert.NotEqual(lStart, lBlue.GetComponent<Kinematic>().Position);

        mPlayer.Stop();

        Assert.Equal(PlaybackState.Stopped, mPlayer.State);
        Assert.Equal(TimeSpan.Zero, mPlayer.Simulation.Elapsed);
        Assert.Equal(lStart, ((TwoFightersScenario)mPlayer.Scenario).Blue.GetComponent<Kinematic>().Position);
    }

    [Fact]
    public void Events_ReportStateAndSimulationChanges()
    {
        List<PlaybackState> lStates = new List<PlaybackState>();
        int lChanges = 0;
        mPlayer.StateChanged += (pSender, pArgs) => lStates.Add(mPlayer.State);
        mPlayer.SimulationChanged += (pSender, pArgs) => lChanges++;

        mPlayer.Play();
        mPlayer.Advance(TimeSpan.FromSeconds(3));
        mPlayer.Pause();
        mPlayer.Stop();

        Assert.Equal(new[] { PlaybackState.Playing, PlaybackState.Paused, PlaybackState.Stopped }, lStates);
        Assert.Equal(2, lChanges);
    }

    [Fact]
    public void Speed_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => mPlayer.Speed = 0);
    }
}
