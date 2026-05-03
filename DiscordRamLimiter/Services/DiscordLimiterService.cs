using System.Diagnostics;
using System.Runtime.InteropServices;
using DiscordRamLimiter.Models;

namespace DiscordRamLimiter.Services;

public sealed class DiscordLimiterService : IDisposable
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan PostTrimRefreshDelay = TimeSpan.FromMilliseconds(180);
    private static readonly string[] DiscordProcessNames =
    [
        "Discord",
        "DiscordCanary",
        "DiscordPTB",
        "DiscordDevelopment"
    ];

    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private long? _initialWorkingSetBytes;
    private bool _disposed;

    public event EventHandler<DiscordMemorySnapshot>? SnapshotUpdated;

    public bool IsLimiterActive { get; private set; }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

    public Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_monitorTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        _initialWorkingSetBytes ??= GetTotalDiscordWorkingSetBytes(out _);
        _monitorCancellation = new CancellationTokenSource();
        _monitorTask = MonitorLoopAsync(_monitorCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        IsLimiterActive = false;

        if (_monitorCancellation is null)
        {
            return;
        }

        await _monitorCancellation.CancelAsync();

        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _monitorCancellation.Dispose();
        _monitorCancellation = null;
        _monitorTask = null;
    }

    public void SetLimiterActive(bool isActive)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsLimiterActive = isActive;
    }

    public int GetLargestDiscordProcessId()
    {
        return GetDiscordProcesses()
            .OrderByDescending(process => SafeWorkingSet(process))
            .Select(process => process.Id)
            .FirstOrDefault(-1);
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var beforeBytes = GetTotalDiscordWorkingSetBytes(out var processCount);

            if (IsLimiterActive && processCount > 0)
            {
                LimitDiscordMemory();

                await Task.Delay(PostTrimRefreshDelay, cancellationToken);
                beforeBytes = GetTotalDiscordWorkingSetBytes(out processCount);
            }

            SnapshotUpdated?.Invoke(
                this,
                new DiscordMemorySnapshot(
                    beforeBytes,
                    _initialWorkingSetBytes ?? 0,
                    processCount,
                    IsLimiterActive,
                    DateTimeOffset.Now));

            await Task.Delay(MonitorInterval, cancellationToken);
        }
    }

    private static void LimitDiscordMemory()
    {
        foreach (var process in GetDiscordProcesses())
        {
            try
            {
                SetProcessWorkingSetSize(process.Handle, -1, -1);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static long GetTotalDiscordWorkingSetBytes(out int processCount)
    {
        long totalBytes = 0;
        processCount = 0;

        foreach (var process in GetDiscordProcesses())
        {
            try
            {
                totalBytes += process.WorkingSet64;
                processCount++;
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return totalBytes;
    }

    private static IEnumerable<Process> GetDiscordProcesses()
    {
        foreach (var processName in DiscordProcessNames)
        {
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var process in processes)
            {
                yield return process;
            }
        }
    }

    private static long SafeWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsLimiterActive = false;
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
    }
}
