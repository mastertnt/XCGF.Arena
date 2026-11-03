using System.Diagnostics;
using XCGF.Arena.Data;
using XCGF.Arena.Dis;
using XCGF.Arena.Scenarios;

namespace XCGF.Arena;

/// <summary>
/// Options of a run of the arena.
/// </summary>
/// <param name="LibraryPath">XCGF.Data library to load.</param>
/// <param name="Scenario">Scenario to play.</param>
/// <param name="Duration">Simulation time to play.</param>
/// <param name="Step">Duration of a simulation step.</param>
/// <param name="RealTime">Whether to pace the steps on the wall clock instead of running as fast as possible.</param>
/// <param name="Dis">Destination of the DIS entity states, or null for no DIS output.</param>
public sealed record ArenaRunOptions(
    string LibraryPath,
    IScenario Scenario,
    TimeSpan Duration,
    TimeSpan Step,
    bool RealTime = false,
    IDisPublisher? Dis = null);

/// <summary>
/// Entry point of the arena, independent of the host that runs it: loads a library, plays a scenario and writes a journal.
/// </summary>
public sealed class ArenaRunner
{
    private const string StartMessage = "XCGF.Arena started.";

    /// <summary>
    /// Runs the arena.
    /// </summary>
    /// <param name="pOptions">What to run.</param>
    /// <param name="pOutput">The writer that receives the arena messages.</param>
    /// <param name="pCancellationToken">The token used to stop the arena.</param>
    /// <returns>A task that completes when the arena has finished.</returns>
    public async Task RunAsync(ArenaRunOptions pOptions, TextWriter pOutput, CancellationToken pCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pOptions);
        ArgumentNullException.ThrowIfNull(pOutput);
        pCancellationToken.ThrowIfCancellationRequested();

        await pOutput.WriteLineAsync(StartMessage.AsMemory(), pCancellationToken).ConfigureAwait(false);
        ArchetypeLibrary lLibrary = XcgfDataLoader.Load(pOptions.LibraryPath);
        ArenaSimulation lArena = new ArenaSimulation(lLibrary);
        if (pOptions.Dis is not null)
        {
            lArena.Systems.Add(new DisSystem(pOptions.Dis));
        }

        ArenaJournal lJournal = new ArenaJournal(pOutput);
        await pOutput.WriteLineAsync($"Scenario: {pOptions.Scenario.Name}".AsMemory(), pCancellationToken).ConfigureAwait(false);
        pOptions.Scenario.Setup(lArena);
        lJournal.Record(lArena);

        Stopwatch lClock = Stopwatch.StartNew();
        while (lArena.Elapsed < pOptions.Duration)
        {
            pCancellationToken.ThrowIfCancellationRequested();
            TimeSpan lRemaining = pOptions.Duration - lArena.Elapsed;
            lArena.Step(lRemaining < pOptions.Step ? lRemaining : pOptions.Step);
            pOptions.Scenario.Update(lArena);
            lJournal.Record(lArena);
            if (pOptions.RealTime && lArena.Elapsed > lClock.Elapsed)
            {
                await Task.Delay(lArena.Elapsed - lClock.Elapsed, pCancellationToken).ConfigureAwait(false);
            }
        }

        await pOutput.WriteLineAsync($"XCGF.Arena stopped after {lArena.Elapsed}.".AsMemory(), pCancellationToken).ConfigureAwait(false);
    }
}
