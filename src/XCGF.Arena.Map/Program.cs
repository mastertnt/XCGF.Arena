using Avalonia;
using XCGF.Arena.Data;
using XCGF.Arena.Scenarios;

namespace XCGF.Arena.Map;

/// <summary>
/// Entry point of the arena map: shows the scenario on an OpenStreetMap background, with play, pause and stop.
/// </summary>
internal static class Program
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Starts the application. Usage: XCGF.Arena.Map [--library &lt;path&gt;] [--scenario scripted|blueprint].
    /// </summary>
    /// <param name="pArguments">The command line arguments.</param>
    [STAThread]
    public static void Main(string[] pArguments)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(pArguments);
    }

    /// <summary>
    /// Builds the Avalonia application, also used by the designer.
    /// </summary>
    /// <returns>The application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }

    /// <summary>
    /// Gets the scenarios offered, the first one by default.
    /// </summary>
    public static IReadOnlyList<ScenarioChoice> Scenarios { get; } = new[]
    {
        new ScenarioChoice("scripted", "Deux chasseurs (script)", () => new TwoFightersScenario()),
        new ScenarioChoice("blueprint", "Deux chasseurs (blueprints)", () => new BlueprintFightersScenario(Console.Out)),
    };

    /// <summary>
    /// Loads the library given by --library, or else the arena library.
    /// </summary>
    /// <param name="pArguments">The command line arguments.</param>
    /// <returns>The archetypes.</returns>
    public static ArchetypeLibrary LoadLibrary(string[] pArguments)
    {
        string lLibrary = OptionValue(pArguments, "--library") ?? Path.Combine(AppContext.BaseDirectory, "ArenaLibrary", "library.xml");
        return XcgfDataLoader.Load(lLibrary);
    }

    /// <summary>
    /// Finds the scenario given by --scenario, or else the first one.
    /// </summary>
    /// <param name="pArguments">The command line arguments.</param>
    /// <returns>The index of the scenario in <see cref="Scenarios"/>.</returns>
    public static int SelectScenario(string[] pArguments)
    {
        string? lKey = OptionValue(pArguments, "--scenario");
        int lIndex = Scenarios.ToList().FindIndex(pChoice => string.Equals(pChoice.Key, lKey, StringComparison.OrdinalIgnoreCase));
        return Math.Max(lIndex, 0);
    }

    /// <summary>
    /// Creates the player of a scenario.
    /// </summary>
    /// <param name="pLibrary">The archetypes.</param>
    /// <param name="pScenario">The scenario.</param>
    /// <returns>The player, stopped.</returns>
    public static ArenaPlayer CreatePlayer(ArchetypeLibrary pLibrary, ScenarioChoice pScenario)
    {
        ArgumentNullException.ThrowIfNull(pScenario);
        return new ArenaPlayer(pLibrary, pScenario.Create, sStep, Console.Out);
    }

    private static string? OptionValue(string[] pArguments, string pOption)
    {
        int lIndex = Array.IndexOf(pArguments, pOption);
        return lIndex >= 0 && lIndex + 1 < pArguments.Length ? pArguments[lIndex + 1] : null;
    }
}
