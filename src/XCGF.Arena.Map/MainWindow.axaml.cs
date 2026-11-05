using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace XCGF.Arena.Map;

/// <summary>
/// Map of the arena with its execution controller.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan sFrame = TimeSpan.FromMilliseconds(50);

    private readonly ArenaPlayer mPlayer;
    private readonly ArenaMapLayer mEntities = new ArenaMapLayer();
    private readonly DispatcherTimer mTimer;
    private readonly Stopwatch mClock = new Stopwatch();

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class, for the designer.
    /// </summary>
    public MainWindow()
        : this(Program.CreatePlayer(Array.Empty<string>()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="pPlayer">The player of the scenario.</param>
    public MainWindow(ArenaPlayer pPlayer)
    {
        ArgumentNullException.ThrowIfNull(pPlayer);
        mPlayer = pPlayer;
        InitializeComponent();
        MapView.Map = mEntities.CreateMap();
        mPlayer.StateChanged += (pSender, pArgs) => Refresh();
        mPlayer.SimulationChanged += (pSender, pArgs) => Refresh();
        mTimer = new DispatcherTimer(sFrame, DispatcherPriority.Background, OnFrame);
        Refresh();
        mEntities.ZoomToEntities(MapView.Map);
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
