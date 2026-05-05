using System.Windows;
using System.Runtime.Versioning;
using DiscordRamLimiter.Services;

namespace DiscordRamLimiter;

[SupportedOSPlatform("windows")]
public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startMinimized = e.Args.Any(arg => string.Equals(arg, StartupService.MinimizedArgument, StringComparison.OrdinalIgnoreCase));
        _mainWindow = new MainWindow(startMinimized);
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        base.OnExit(e);
    }
}
