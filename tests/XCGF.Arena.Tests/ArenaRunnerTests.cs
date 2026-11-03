namespace XCGF.Arena.Tests;

public class ArenaRunnerTests
{
    [Fact]
    public async Task RunAsync_WritesStartMessage()
    {
        ArenaRunner lRunner = new ArenaRunner();
        using StringWriter lOutput = new StringWriter();

        await lRunner.RunAsync(lOutput, CancellationToken.None);

        Assert.Contains("XCGF.Arena started.", lOutput.ToString());
    }

    [Fact]
    public async Task RunAsync_WithNullOutput_Throws()
    {
        ArenaRunner lRunner = new ArenaRunner();

        await Assert.ThrowsAsync<ArgumentNullException>(() => lRunner.RunAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ThrowsAndWritesNothing()
    {
        ArenaRunner lRunner = new ArenaRunner();
        using StringWriter lOutput = new StringWriter();
        using CancellationTokenSource lCancellation = new CancellationTokenSource();
        lCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lRunner.RunAsync(lOutput, lCancellation.Token));

        Assert.Empty(lOutput.ToString());
    }
}
