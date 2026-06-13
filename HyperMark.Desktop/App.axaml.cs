using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using HyperMark.Config;

namespace HyperMark.Desktop;

public class App : Application
{
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _toggleItem;
    private NativeMenuItem? _autoStartItem;
    private bool _isRunning = true;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        CreateTrayIcon();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            lifetime.ShutdownRequested += (_, _) => _trayIcon?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon()
    {
        try
        {
            var iconStream = AssetLoader.Open(new Uri("avares://HyperMark.Desktop/Resources/app.ico"));

            var menu = new NativeMenu();

            _statusItem = new NativeMenuItem("✓ 服务运行中") { IsEnabled = false };
            menu.Add(_statusItem);
            menu.Add(new NativeMenuItemSeparator());

            _toggleItem = new NativeMenuItem("暂停服务");
            _toggleItem.Click += async (_, _) => await ToggleService();
            menu.Add(_toggleItem);

            var openWebItem = new NativeMenuItem("打开 Web 界面");
            openWebItem.Click += (_, _) => OpenWebInterface();
            menu.Add(openWebItem);

            var openDataItem = new NativeMenuItem("打开数据目录");
            openDataItem.Click += (_, _) => OpenDataDirectory();
            menu.Add(openDataItem);

            menu.Add(new NativeMenuItemSeparator());

            var settingsMenu = new NativeMenu();
            _autoStartItem = new NativeMenuItem("开机自动启动")
            {
                IsChecked = StartupManager.IsAutoStartEnabled()
            };
            _autoStartItem.Click += (_, _) => ToggleAutoStart();
            settingsMenu.Add(_autoStartItem);

            var settingsItem = new NativeMenuItem("设置");
            settingsItem.Menu = settingsMenu;
            menu.Add(settingsItem);

            menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) => ExitApp();
            menu.Add(exitItem);

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(iconStream),
                ToolTipText = "HyperMark - 网页收藏服务",
                IsVisible = true,
                Menu = menu
            };

            _trayIcon.Clicked += (_, _) => OpenWebInterface();

            TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"创建托盘图标失败: {ex.Message}");
        }
    }

    private async Task ToggleService()
    {
        if (Program.App == null) return;

        try
        {
            if (_isRunning)
            {
                await Program.App.StopAsync();
                _isRunning = false;

                if (_toggleItem != null) _toggleItem.Header = "启动服务";
                if (_statusItem != null) _statusItem.Header = "⏸ 服务已停止";
                if (_trayIcon != null) _trayIcon.ToolTipText = "HyperMark - 服务已停止";
            }
            else
            {
                await Program.App.StartAsync();
                _isRunning = true;

                if (_toggleItem != null) _toggleItem.Header = "暂停服务";
                if (_statusItem != null) _statusItem.Header = "✓ 服务运行中";
                if (_trayIcon != null) _trayIcon.ToolTipText = "HyperMark - 网页收藏服务";
            }
        }
        catch { }
    }

    private void ToggleAutoStart()
    {
        if (_autoStartItem == null) return;
        var current = StartupManager.IsAutoStartEnabled();
        StartupManager.SetAutoStart(!current);
        _autoStartItem.IsChecked = !current;
    }

    private void OpenWebInterface()
    {
        if (!_isRunning) return;
        try
        {
            var url = AppConfig.Instance.Http;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenDataDirectory()
    {
        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hypermark");

            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            Process.Start(new ProcessStartInfo(dataDir) { UseShellExecute = true });
        }
        catch { }
    }

    private async void ExitApp()
    {
        _trayIcon?.Dispose();

        // 主动停止 Web 服务，避免 finally 块中卡住
        if (Program.App != null)
        {
            try
            {
                await Program.App.StopAsync();
            }
            catch { }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }
}
