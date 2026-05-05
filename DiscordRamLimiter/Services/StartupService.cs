using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DiscordRamLimiter.Services;

public static class StartupService
{
    public const string MinimizedArgument = "--minimized";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DiscordRamLimiter";

    public static bool IsLaunchAtStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(RunValueName) as string, GetStartupCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetLaunchAtStartup(bool isEnabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (isEnabled)
        {
            key.SetValue(RunValueName, GetStartupCommand(), RegistryValueKind.String);
            return;
        }

        key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private static string GetStartupCommand()
    {
        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            processPath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Unable to resolve the application path.");
        }

        return $"\"{processPath}\" {MinimizedArgument}";
    }
}
