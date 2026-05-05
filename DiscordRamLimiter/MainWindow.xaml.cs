using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DiscordRamLimiter.Models;
using DiscordRamLimiter.Services;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;
using Imaging = System.Drawing.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace DiscordRamLimiter;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window, IDisposable
{
    private readonly DiscordLimiterService _limiterService = new();
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Drawing.Icon? _customTrayIcon;
    private readonly bool _startMinimized;
    private readonly Forms.ToolStripMenuItem _startupMenuItem;
    private bool _isExitRequested;
    private bool _isMinimizingToTray;

    public MainWindow(bool startMinimized = false)
    {
        _startMinimized = startMinimized;

        InitializeComponent();
        ApplyWindowIcon();

        _customTrayIcon = LoadTrayIcon();
        _trayIcon = BuildTrayIcon(_customTrayIcon, out _startupMenuItem);
        _limiterService.SnapshotUpdated += LimiterService_SnapshotUpdated;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _limiterService.StartAsync();
        _limiterService.SetLimiterActive(_startMinimized);
        AnimateToggle(_startMinimized, durationMs: 1);
        UpdateStatus(_startMinimized);
        RefreshStartupState();

        if (_startMinimized)
        {
            MinimizeToTray(showNotification: false);
        }
    }

    private void LimiterToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var isActive = !_limiterService.IsLimiterActive;
        _limiterService.SetLimiterActive(isActive);
        AnimateToggle(isActive, durationMs: 240);
        UpdateStatus(isActive);
    }

    private void LimiterService_SnapshotUpdated(object? sender, DiscordMemorySnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentRamText.Text = FormatBytes(snapshot.CurrentWorkingSetBytes);

            ProcessCountText.Text = snapshot.ProcessCount switch
            {
                0 => "No Discord process",
                1 => "1 Discord process",
                _ => $"{snapshot.ProcessCount} Discord processes"
            };

            UpdateStatus(snapshot.IsLimiterActive);
        });
    }

    private void UpdateStatus(bool isActive)
    {
        StatusText.Text = isActive ? "Limiter Active" : "Limiter Disabled";
        StatusText.Foreground = new SolidColorBrush(isActive ? MediaColor.FromRgb(86, 217, 143) : MediaColor.FromRgb(255, 143, 155));
    }

    private void AnimateToggle(bool isActive, int durationMs)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        ToggleThumbLabel.Text = isActive ? "ON" : "OFF";
        ToggleThumbLabel.Foreground = new SolidColorBrush(isActive ? MediaColor.FromRgb(25, 146, 85) : MediaColor.FromRgb(218, 75, 90));

        ToggleTrackBrush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new ColorAnimation
            {
                To = isActive ? MediaColor.FromRgb(54, 206, 125) : MediaColor.FromRgb(155, 66, 76),
                Duration = duration,
                EasingFunction = easing
            });

        ToggleTrackBorderBrush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new ColorAnimation
            {
                To = isActive ? MediaColor.FromRgb(86, 217, 143) : MediaColor.FromRgb(70, 55, 67),
                Duration = duration,
                EasingFunction = easing
            });

        ToggleGlowEffect.BeginAnimation(
            DropShadowEffect.OpacityProperty,
            new DoubleAnimation
            {
                To = isActive ? 0.55 : 0,
                Duration = duration,
                EasingFunction = easing
            });

        ToggleGlowEffect.BeginAnimation(
            DropShadowEffect.BlurRadiusProperty,
            new DoubleAnimation
            {
                To = isActive ? 34 : 18,
                Duration = duration,
                EasingFunction = easing
            });

        ToggleThumbTransform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation
            {
                To = isActive ? 140 : 0,
                Duration = duration,
                EasingFunction = easing
            });
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 MB";
        }

        var megabytes = bytes / 1024d / 1024d;
        return megabytes >= 100
            ? $"{megabytes:0} MB"
            : $"{megabytes:0.0} MB";
    }

    private Forms.NotifyIcon BuildTrayIcon(Drawing.Icon? trayIcon, out Forms.ToolStripMenuItem startupMenuItem)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        startupMenuItem = new Forms.ToolStripMenuItem("Launch at startup")
        {
            CheckOnClick = true
        };
        startupMenuItem.CheckedChanged += StartupMenuItem_CheckedChanged;
        menu.Items.Add(startupMenuItem);
        menu.Items.Add("Exit", null, async (_, _) => await ExitApplicationAsync());

        var notifyIcon = new Forms.NotifyIcon
        {
            Text = "Discord RAM Limiter",
            Icon = trayIcon ?? Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        notifyIcon.DoubleClick += (_, _) => ShowFromTray();
        return notifyIcon;
    }

    private void AutoStartCheckBox_Click(object sender, RoutedEventArgs e)
    {
        SetLaunchAtStartup(AutoStartCheckBox.IsChecked == true);
    }

    private void StartupMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        SetLaunchAtStartup(_startupMenuItem.Checked);
    }

    private void RefreshStartupState()
    {
        var isEnabled = StartupService.IsLaunchAtStartupEnabled();
        AutoStartCheckBox.IsChecked = isEnabled;
        _startupMenuItem.CheckedChanged -= StartupMenuItem_CheckedChanged;
        _startupMenuItem.Checked = isEnabled;
        _startupMenuItem.CheckedChanged += StartupMenuItem_CheckedChanged;
    }

    private void SetLaunchAtStartup(bool isEnabled)
    {
        try
        {
            StartupService.SetLaunchAtStartup(isEnabled);
            RefreshStartupState();
        }
        catch (UnauthorizedAccessException)
        {
            RefreshStartupState();
            ShowStartupError();
        }
        catch (IOException)
        {
            RefreshStartupState();
            ShowStartupError();
        }
        catch (SecurityException)
        {
            RefreshStartupState();
            ShowStartupError();
        }
    }

    private void ShowStartupError()
    {
        _trayIcon.BalloonTipTitle = "Discord RAM Limiter";
        _trayIcon.BalloonTipText = "Could not update Windows startup settings.";
        _trayIcon.BalloonTipIcon = Forms.ToolTipIcon.Warning;
        _trayIcon.ShowBalloonTip(2200);
    }

    private static string GetAppIconPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "discord_no_logo.ico");
    }

    private void ApplyWindowIcon()
    {
        var iconPath = GetAppIconPath();
        if (File.Exists(iconPath))
        {
            Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
            return;
        }

        using var appIcon = LoadExecutableIcon();
        if (appIcon is not null)
        {
            var imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                appIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            imageSource.Freeze();
            Icon = imageSource;
        }
    }

    private static Drawing.Icon? LoadTrayIcon()
    {
        var iconPath = GetAppIconPath();
        var traySize = Forms.SystemInformation.SmallIconSize;
        using var sourceIcon = File.Exists(iconPath)
            ? new Drawing.Icon(iconPath, traySize)
            : LoadExecutableIcon();

        if (sourceIcon is null)
        {
            return null;
        }

        using var sourceBitmap = sourceIcon.ToBitmap();
        using var trayBitmap = new Drawing.Bitmap(traySize.Width, traySize.Height, Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Drawing.Graphics.FromImage(trayBitmap);
        var sourceBounds = FindVisibleBounds(sourceBitmap);

        graphics.Clear(Drawing.Color.Transparent);
        graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;

        var scale = Math.Min(
            1d,
            Math.Min((double)traySize.Width / sourceBounds.Width, (double)traySize.Height / sourceBounds.Height));
        var drawWidth = (int)Math.Round(sourceBounds.Width * scale);
        var drawHeight = (int)Math.Round(sourceBounds.Height * scale);
        var x = (traySize.Width - drawWidth) / 2;
        var y = (traySize.Height - drawHeight) / 2;
        graphics.DrawImage(
            sourceBitmap,
            new Drawing.Rectangle(x, y, drawWidth, drawHeight),
            sourceBounds,
            Drawing.GraphicsUnit.Pixel);

        return CreateIconFromBitmap(trayBitmap);
    }

    private static Drawing.Icon? LoadExecutableIcon()
    {
        return Environment.ProcessPath is { Length: > 0 } processPath && File.Exists(processPath)
            ? Drawing.Icon.ExtractAssociatedIcon(processPath)
            : null;
    }

    private static Drawing.Rectangle FindVisibleBounds(Drawing.Bitmap bitmap)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX >= 0
            ? Drawing.Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)
            : new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
    }

    private static Drawing.Icon CreateIconFromBitmap(Drawing.Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && !_isExitRequested && IsVisible)
        {
            MinimizeToTray();
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTray();
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        await ExitApplicationAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        MinimizeToTray();
    }

    private void MinimizeToTray(bool showNotification = true)
    {
        if (_isMinimizingToTray || !IsVisible)
        {
            return;
        }

        _isMinimizingToTray = true;
        try
        {
            WindowState = WindowState.Minimized;
            Hide();
            if (showNotification)
            {
                ShowTrayNotification();
            }
        }
        finally
        {
            _isMinimizingToTray = false;
        }
    }

    private void ShowTrayNotification()
    {
        _trayIcon.BalloonTipTitle = "Discord RAM Limiter";
        _trayIcon.BalloonTipText = "Still running in the system tray.";
        _trayIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(2200);
    }

    private async Task ExitApplicationAsync()
    {
        _isExitRequested = true;
        _trayIcon.Visible = false;
        await _limiterService.StopAsync();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
        _customTrayIcon?.Dispose();
        _limiterService.Dispose();
    }
}
