namespace XCGF.Arena.ConsoleHost;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int CancelledExitCode = 130;

    private static async Task<int> Main()
    {
        using CancellationTokenSource lCancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (pSender, pEventArgs) =>
        {
            // Let the arena stop by itself instead of having the runtime kill the process.
            pEventArgs.Cancel = true;
            lCancellation.Cancel();
        };

        ArenaRunner lRunner = new ArenaRunner();
        try
        {
            await lRunner.RunAsync(Console.Out, lCancellation.Token);
            return SuccessExitCode;
        }
        catch (OperationCanceledException lException)
        {
            await Console.Error.WriteLineAsync($"XCGF.Arena stopped: {lException.Message}");
            return CancelledExitCode;
        }
    }
}
