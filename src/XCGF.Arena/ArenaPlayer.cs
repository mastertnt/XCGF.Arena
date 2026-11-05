using XCGF.Arena.Data;
using XCGF.Arena.Scenarios;

namespace XCGF.Arena;

/// <summary>
/// State of an <see cref="ArenaPlayer"/>.
/// </summary>
public enum PlaybackState
{
    /// <summary>The scenario is set up at its start and time does not advance.</summary>
    Stopped = 0,

    /// <summary>Time advances.</summary>
    Playing = 1,

    /// <summary>Time is frozen where it was paused.</summary>
    Paused = 2,
}

/// <summary>
/// Play, pause and stop control of a scenario, independent of the host that drives it: the host calls
/// <see cref="Advance"/> with the wall-clock time elapsed, and the player runs the simulation steps that are due.
/// </summary>
/// <remarks>
/// Stop discards the simulation and sets the scenario up again, so that the next Play starts from the beginning.
/// </remarks>
public sealed class ArenaPlayer
{
    private readonly ArchetypeLibrary mLibrary;
    private readonly Func<IScenario> mScenarioFactory;
    private readonly TextWriter mJournalOutput;
    private ArenaJournal mJournal;
    private TimeSpan mPending;
    private double mSpeed = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArenaPlayer"/> class, stopped at the start of the scenario.
    /// </summary>
    /// <param name="pLibrary">The archetypes.</param>
    /// <param name="pScenarioFactory">Creates a fresh scenario, at construction and at each stop.</param>
    /// <param name="pStep">The duration of a simulation step. It must be positive.</param>
    /// <param name="pJournalOutput">The writer that receives the journal of the simulation, or null for none.</param>
    public ArenaPlayer(ArchetypeLibrary pLibrary, Func<IScenario> pScenarioFactory, TimeSpan pStep, TextWriter? pJournalOutput = null)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        ArgumentNullException.ThrowIfNull(pScenarioFactory);
        if (pStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pStep), pStep, "The step duration must be positive.");
        }

        mLibrary = pLibrary;
        mScenarioFactory = pScenarioFactory;
        mJournalOutput = pJournalOutput ?? TextWriter.Null;
        Step = pStep;
        (Simulation, Scenario, mJournal) = SetUp();
    }

    /// <summary>
    /// Occurs when <see cref="State"/> changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Occurs when the simulation has changed: after the steps run by <see cref="Advance"/>, and after a stop.
    /// </summary>
    public event EventHandler? SimulationChanged;

    /// <summary>
    /// Gets the playback state.
    /// </summary>
    public PlaybackState State { get; private set; }

    /// <summary>
    /// Gets the current simulation. A stop replaces it.
    /// </summary>
    public ArenaSimulation Simulation { get; private set; }

    /// <summary>
    /// Gets the scenario played by the current simulation.
    /// </summary>
    public IScenario Scenario { get; private set; }

    /// <summary>
    /// Gets the duration of a simulation step.
    /// </summary>
    public TimeSpan Step { get; }

    /// <summary>
    /// Gets or sets the number of simulated seconds per wall-clock second. It must be positive.
    /// </summary>
    public double Speed
    {
        get => mSpeed;
        set
        {
            if (!(value > 0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The speed must be positive.");
            }

            mSpeed = value;
        }
    }

    /// <summary>
    /// Starts or resumes the scenario.
    /// </summary>
    public void Play()
    {
        SetState(PlaybackState.Playing);
    }

    /// <summary>
    /// Freezes the scenario where it is. Does nothing when stopped.
    /// </summary>
    public void Pause()
    {
        if (State == PlaybackState.Playing)
        {
            SetState(PlaybackState.Paused);
        }
    }

    /// <summary>
    /// Stops the scenario and sets it up again at its start.
    /// </summary>
    public void Stop()
    {
        if (State == PlaybackState.Stopped)
        {
            return;
        }

        mPending = TimeSpan.Zero;
        (Simulation, Scenario, mJournal) = SetUp();
        SetState(PlaybackState.Stopped);
        SimulationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Runs the steps due after some wall-clock time, when playing.
    /// </summary>
    /// <param name="pWallElapsed">The wall-clock time elapsed since the previous call. It must not be negative.</param>
    /// <returns>The number of steps run.</returns>
    public int Advance(TimeSpan pWallElapsed)
    {
        if (pWallElapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pWallElapsed), pWallElapsed, "The elapsed time must not be negative.");
        }

        if (State != PlaybackState.Playing)
        {
            return 0;
        }

        mPending += pWallElapsed * Speed;
        int lSteps = 0;
        while (mPending >= Step)
        {
            mPending -= Step;
            Simulation.Step(Step);
            Scenario.Update(Simulation);
            mJournal.Record(Simulation);
            lSteps++;
        }

        if (lSteps > 0)
        {
            SimulationChanged?.Invoke(this, EventArgs.Empty);
        }

        return lSteps;
    }

    private (ArenaSimulation Simulation, IScenario Scenario, ArenaJournal Journal) SetUp()
    {
        ArenaSimulation lSimulation = new ArenaSimulation(mLibrary);
        IScenario lScenario = mScenarioFactory();
        ArenaJournal lJournal = new ArenaJournal(mJournalOutput);
        lScenario.Setup(lSimulation);
        lJournal.Record(lSimulation);
        return (lSimulation, lScenario, lJournal);
    }

    private void SetState(PlaybackState pState)
    {
        if (State != pState)
        {
            State = pState;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
