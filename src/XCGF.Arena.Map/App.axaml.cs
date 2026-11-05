using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace XCGF.Arena.Map;

/// <summary>
/// Avalonia application of the arena map.
/// </summary>
public sealed class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lDesktop)
        {
            string[] lArguments = lDesktop.Args ?? Array.Empty<string>();
            lDesktop.MainWindow = new MainWindow(Program.LoadLibrary(lArguments), Program.SelectScenario(lArguments));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
