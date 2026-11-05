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
    /// Starts the application. Usage: XCGF.Arena.Map [--library &lt;path&gt;].
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
    /// Creates the player of the two fighters scenario, on the library given by --library or on the arena library.
    /// </summary>
    /// <param name="pArguments">The command line arguments.</param>
    /// <returns>The player, stopped.</returns>
    public static ArenaPlayer CreatePlayer(string[] pArguments)
    {
        string lLibrary = Path.Combine(AppContext.BaseDirectory, "ArenaLibrary", "library.xml");
        int lIndex = Array.IndexOf(pArguments, "--library");
        if (lIndex >= 0 && lIndex + 1 < pArguments.Length)
        {
            lLibrary = pArguments[lIndex + 1];
        }

        ArchetypeLibrary lArchetypes = XcgfDataLoader.Load(lLibrary);
        return new ArenaPlayer(lArchetypes, () => new TwoFightersScenario(), sStep, Console.Out);
    }
}
