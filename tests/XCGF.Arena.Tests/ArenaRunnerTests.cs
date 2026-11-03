using XCGF.Arena.Dis;
using XCGF.Arena.Scenarios;

namespace XCGF.Arena.Tests;

public class ArenaRunnerTests
{
    [Fact]
    public async Task RunAsync_TwoFighters_WritesJournalOfTheScenario()
    {
        ArenaRunner lRunner = new ArenaRunner();
        using StringWriter lOutput = new StringWriter();

        await lRunner.RunAsync(Options(TimeSpan.FromMinutes(11)), lOutput, CancellationToken.None);

        string[] lLines = lOutput.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("XCGF.Arena started.", lLines[0]);
        Assert.Contains("[00:00:01] BLUE1.Radar detects RED1", lLines);
        Assert.Contains("[00:00:01] RED1.Radar detects BLUE1", lLines);
        Assert.Contains(lLines, pLine => pLine.EndsWith("BLUE1 FireOnTarget started", StringComparison.Ordinal));
        Assert.Contains(lLines, pLine => pLine.EndsWith("BLUE1 FireOnTarget ended (Aborted)", StringComparison.Ordinal));
        Assert.Contains(lLines, pLine => pLine.EndsWith("BLUE1.Radar loses RED1", StringComparison.Ordinal));
        Assert.Contains(lLines, pLine => pLine.EndsWith("RED1 MoveTo ended (Success)", StringComparison.Ordinal));
        Assert.Equal("XCGF.Arena stopped after 00:11:00.", lLines[^1]);
    }

    [Fact]
    public async Task RunAsync_WithDis_PublishesBothFightersEverySecond()
    {
        RecordingPublisher lPublisher = new RecordingPublisher();
        ArenaRunner lRunner = new ArenaRunner();

        await lRunner.RunAsync(Options(TimeSpan.FromSeconds(10)) with { Dis = lPublisher }, TextWriter.Null, CancellationToken.None);

        Assert.Equal(20, lPublisher.States.Count);
        Assert.Equal(new[] { "BLUE1", "RED1" }, lPublisher.States.Select(pState => pState.Marking).Distinct().Order());
    }

    [Fact]
    public async Task RunAsync_WithNullOutput_Throws()
    {
        ArenaRunner lRunner = new ArenaRunner();

        await Assert.ThrowsAsync<ArgumentNullException>(() => lRunner.RunAsync(Options(TimeSpan.FromSeconds(1)), null!, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ThrowsAndWritesNothing()
    {
        ArenaRunner lRunner = new ArenaRunner();
        using StringWriter lOutput = new StringWriter();
        using CancellationTokenSource lCancellation = new CancellationTokenSource();
        lCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lRunner.RunAsync(Options(TimeSpan.FromSeconds(1)), lOutput, lCancellation.Token));

        Assert.Empty(lOutput.ToString());
    }

    private static ArenaRunOptions Options(TimeSpan pDuration)
    {
        return new ArenaRunOptions(TestLibrary.PathOf("library.xml"), new TwoFightersScenario(), pDuration, TimeSpan.FromSeconds(1));
    }

    private sealed class RecordingPublisher : IDisPublisher
    {
        public List<DisEntityState> States { get; } = new List<DisEntityState>();

        public void Publish(DisEntityState pState) => States.Add(pState);
    }
}
