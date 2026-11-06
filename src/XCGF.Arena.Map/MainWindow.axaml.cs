using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Friflo.Engine.ECS;
using XCGF.Arena.Blueprints;
using XCGF.Arena.Data;
using XCGF.Arena.Map.Blueprints;

namespace XCGF.Arena.Map;

/// <summary>
/// Map of the arena with its execution controller and, when the scenario uses blueprints, the graph of each entity with
/// its running fibers and the log of the blueprints.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan sFrame = TimeSpan.FromMilliseconds(50);
    private const int MaxLogLines = 2000;

    private readonly ArchetypeLibrary mLibrary;
    private readonly ArenaMapLayer mEntities = new ArenaMapLayer();
    private readonly DispatcherTimer mTimer;
    private readonly Stopwatch mClock = new Stopwatch();
    private readonly ObservableCollection<string> mLogs = new ObservableCollection<string>();
    private readonly List<BlueprintDrawing> mDrawings = new List<BlueprintDrawing>();
    private ArenaPlayer mPlayer;
    private ArenaSimulation? mShown;
    private bool mLogAdded;

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
        LogList.ItemsSource = mLogs;
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
        RefreshBlueprint();
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
        if (!ReferenceEquals(mShown, mPlayer.Simulation))
        {
            ShowBlueprints(mPlayer.Simulation);
        }

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
        RefreshBlueprint();
    }

    // A new simulation (scenario chosen, or stopped and set up again): one drawing per entity driven by a graph.
    private void ShowBlueprints(ArenaSimulation pSimulation)
    {
        if (mShown is not null)
        {
            mShown.Blueprints.Logged -= OnBlueprintLogged;
            mShown.Blueprints.NodeExecuted -= OnNodeExecuted;
        }

        mShown = pSimulation;
        mLogs.Clear();
        mDrawings.Clear();
        BlueprintRuntime lRuntime = pSimulation.Blueprints;
        foreach (Entity lOwner in pSimulation.Store.Query<BlueprintRef>().Entities)
        {
            int lGraphId = lOwner.GetComponent<BlueprintRef>().GraphId;
            mDrawings.Add(new BlueprintDrawing(lOwner, lGraphId, lRuntime.Graphs[lGraphId]));
        }

        lRuntime.Logged += OnBlueprintLogged;
        lRuntime.NodeExecuted += OnNodeExecuted;

        bool lVisible = mDrawings.Count > 0;
        BlueprintPanel.IsVisible = lVisible;
        BlueprintSplitter.IsVisible = lVisible;
        Body.ColumnDefinitions[1].Width = lVisible ? new GridLength(4) : new GridLength(0);
        Body.ColumnDefinitions[2].Width = lVisible ? new GridLength(3, GridUnitType.Star) : new GridLength(0);

        // Keep the graph of the same entity when the simulation is set up again.
        int lPrevious = GraphBox.SelectedIndex;
        GraphBox.ItemsSource = mDrawings.ToList();
        GraphBox.SelectedIndex = lVisible ? Math.Clamp(lPrevious, 0, mDrawings.Count - 1) : -1;
    }

    private void OnGraphChanged(object? pSender, SelectionChangedEventArgs pArgs)
    {
        GraphEditor.DrawingSource = (GraphBox.SelectedItem as BlueprintDrawing)?.Drawing;
        RefreshBlueprint();
        FitGraph();
    }

    private void OnFitGraph(object? pSender, RoutedEventArgs pArgs) => FitGraph();

    private void FitGraph()
    {
        // Once the layout of the new drawing is done.
        Dispatcher.UIThread.Post(() => GraphEditor.ZoomControl?.Uniform(), DispatcherPriority.Background);
    }

    private void OnBlueprintLogged(string pLine)
    {
        mLogs.Add(pLine);
        if (mLogs.Count > MaxLogLines)
        {
            mLogs.RemoveAt(0);
        }

        mLogAdded = true;
    }

    private void OnNodeExecuted(EventKey pKey, int pNode)
    {
        foreach (BlueprintDrawing lDrawing in mDrawings)
        {
            if (lDrawing.Owner.Id == pKey.OwnerId && lDrawing.GraphId == pKey.GraphId)
            {
                lDrawing.Flash(pNode);
            }
        }
    }

    // Also while paused or between two steps, so that the halos fade out.
    private void RefreshBlueprint()
    {
        if (GraphBox.SelectedItem is BlueprintDrawing lDrawing)
        {
            lDrawing.Update(mPlayer.Simulation.Store, mPlayer.Simulation.Elapsed);
        }

        if (mLogAdded && mLogs.Count > 0)
        {
            LogList.ScrollIntoView(mLogs.Count - 1);
            mLogAdded = false;
        }
    }
}
