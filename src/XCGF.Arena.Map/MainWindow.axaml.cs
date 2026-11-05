using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XCGF.Arena.Data;

namespace XCGF.Arena.Map;

/// <summary>
/// Map of the arena with its execution controller.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan sFrame = TimeSpan.FromMilliseconds(50);

    private readonly ArchetypeLibrary mLibrary;
    private readonly ArenaMapLayer mEntities = new ArenaMapLayer();
    private readonly DispatcherTimer mTimer;
    private readonly Stopwatch mClock = new Stopwatch();
    private ArenaPlayer mPlayer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class, for the designer.
    /// </summary>
    public MainWindow()
        : this(Program.LoadLibrary(Array.Empty<string>()), 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="pLibrary">The archetypes.</param>
    /// <param name="pScenario">The index of the first scenario shown, in <see cref="Program.Scenarios"/>.</param>
    public MainWindow(ArchetypeLibrary pLibrary, int pScenario)
    {
        ArgumentNullException.ThrowIfNull(pLibrary);
        mLibrary = pLibrary;
        InitializeComponent();
        MapView.Map = mEntities.CreateMap();
        mTimer = new DispatcherTimer(sFrame, DispatcherPriority.Background, OnFrame);
        mPlayer = CreatePlayer(Program.Scenarios[pScenario]);
        ScenarioBox.ItemsSource = Program.Scenarios;
        ScenarioBox.SelectedIndex = pScenario;
        ScenarioBox.SelectionChanged += OnScenarioChanged;
        Refresh();
        mEntities.ZoomToEntities(MapView.Map);
    }

    private ArenaPlayer CreatePlayer(ScenarioChoice pScenario)
    {
        ArenaPlayer lPlayer = Program.CreatePlayer(mLibrary, pScenario);
        lPlayer.StateChanged += (pSender, pArgs) => Refresh();
        lPlayer.SimulationChanged += (pSender, pArgs) => Refresh();
        if (SpeedBox?.SelectedItem is ComboBoxItem { Tag: string lTag })
        {
            lPlayer.Speed = double.Parse(lTag, CultureInfo.InvariantCulture);
        }

        return lPlayer;
    }

    private void OnScenarioChanged(object? pSender, SelectionChangedEventArgs pArgs)
    {
        if (ScenarioBox.SelectedItem is not ScenarioChoice lScenario)
        {
            return;
        }

        mTimer.Stop();
        mPlayer.Stop();
        mPlayer = CreatePlayer(lScenario);
        Refresh();
        mEntities.ZoomToEntities(MapView.Map!);
    }

    private void OnFrame(object? pSender, EventArgs pArgs)
    {
        TimeSpan lElapsed = mClock.Elapsed;
        mClock.Restart();
        mPlayer.Advance(lElapsed);
    }

    private void OnPlay(object? pSender, RoutedEventArgs pArgs)
    {
        mPlayer.Play();
        mClock.Restart();
        mTimer.Start();
    }

    private void OnPause(object? pSender, RoutedEventArgs pArgs)
    {
        mPlayer.Pause();
        mTimer.Stop();
    }

    private void OnStop(object? pSender, RoutedEventArgs pArgs)
    {
        mTimer.Stop();
        mPlayer.Stop();
    }

    private void OnSpeedChanged(object? pSender, SelectionChangedEventArgs pArgs)
    {
        if (SpeedBox?.SelectedItem is ComboBoxItem { Tag: string lTag } && mPlayer is not null)
        {
            mPlayer.Speed = double.Parse(lTag, CultureInfo.InvariantCulture);
        }
    }

    private void Refresh()
    {
        mEntities.Update(mPlayer.Simulation);
        ClockText.Text = string.Create(CultureInfo.InvariantCulture, $"T+{mPlayer.Simulation.Elapsed:hh\\:mm\\:ss}");
        StateText.Text = mPlayer.State switch
        {
            PlaybackState.Playing => "En cours",
            PlaybackState.Paused => "En pause",
            _ => "Arrêté",
        };
        PlayButton.IsEnabled = mPlayer.State != PlaybackState.Playing;
        PauseButton.IsEnabled = mPlayer.State == PlaybackState.Playing;
        StopButton.IsEnabled = mPlayer.State != PlaybackState.Stopped;
    }
}
