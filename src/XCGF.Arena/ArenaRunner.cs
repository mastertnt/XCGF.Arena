namespace XCGF.Arena;

/// <summary>
/// Entry point of the arena, independent of the host that runs it.
/// </summary>
public sealed class ArenaRunner
{
    private const string StartMessage = "XCGF.Arena started.";

    /// <summary>
    /// Runs the arena.
    /// </summary>
    /// <param name="pOutput">The writer that receives the arena messages.</param>
    /// <param name="pCancellationToken">The token used to stop the arena.</param>
    /// <returns>A task that completes when the arena has finished.</returns>
    public async Task RunAsync(TextWriter pOutput, CancellationToken pCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pOutput);
        pCancellationToken.ThrowIfCancellationRequested();

        await pOutput.WriteLineAsync(StartMessage.AsMemory(), pCancellationToken).ConfigureAwait(false);
    }
}
