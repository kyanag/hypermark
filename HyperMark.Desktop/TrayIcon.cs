using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using HyperMark.Config;
using Microsoft.AspNetCore.Builder;

namespace HyperMark.Desktop;

/// <summary>
/// 系统托盘图标管理器
/// </summary>
public class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private readonly WebApplication _app;
    private bool _isRunning = true;
    private bool _disposed = false;

    // 菜单项引用
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _toggleItem;
    private ToolStripMenuItem? _autoStartItem;

    public TrayIcon(WebApplication app)
    {
        _app = app;
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _contextMenu = new ContextMenuStrip();

        // 状态项（不可点击）
        _statusItem = new ToolStripMenuItem("✓ 服务运行中")
        {
            Enabled = false,
            ForeColor = Color.Green
        };
        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 服务开关按钮
        _toggleItem = new ToolStripMenuItem("暂停服务");
        _toggleItem.Click += async (s, e) => await ToggleService();
        _contextMenu.Items.Add(_toggleItem);

        // 打开 Web 界面
        var openWebItem = new ToolStripMenuItem("打开 Web 界面");
        openWebItem.Click += (s, e) => OpenWebInterface();
        _contextMenu.Items.Add(openWebItem);

        // 打开数据目录
        var openDataDirItem = new ToolStripMenuItem("打开数据目录");
        openDataDirItem.Click += (s, e) => OpenDataDirectory();
        _contextMenu.Items.Add(openDataDirItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 设置子菜单
        var settingsMenu = new ToolStripMenuItem("设置");

        // 开机自动启动
        _autoStartItem = new ToolStripMenuItem("开机自动启动")
        {
            Checked = StartupManager.IsAutoStartEnabled()
        };
        _autoStartItem.Click += (s, e) => ToggleAutoStart();
        settingsMenu.DropDownItems.Add(_autoStartItem);

        _contextMenu.Items.Add(settingsMenu);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 退出
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += async (s, e) => await ExitApplication();
        _contextMenu.Items.Add(exitItem);

        // 创建托盘图标
        _notifyIcon = new NotifyIcon
        {
            Icon = GetApplicationIcon(),
            Text = "HyperMark - 网页收藏服务",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // 双击打开 Web 界面
        _notifyIcon.DoubleClick += (s, e) => OpenWebInterface();
    }

    /// <summary>
    /// 获取应用图标
    /// </summary>
    private Icon GetApplicationIcon()
    {
        // 尝试从嵌入资源加载图标
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "HyperMark.Desktop.Resources.app.ico";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            return new Icon(stream);
        }

        // 如果没有自定义图标，使用系统默认图标
        return SystemIcons.Application;
    }

    /// <summary>
    /// 切换服务状态
    /// </summary>
    private async Task ToggleService()
    {
        if (_isRunning)
        {
            // 暂停服务
            await _app.StopAsync();
            _isRunning = false;

            // 更新 UI
            if (_toggleItem != null) _toggleItem.Text = "启动服务";
            if (_statusItem != null)
            {
                _statusItem.Text = "⏸ 服务已停止";
                _statusItem.ForeColor = Color.Gray;
            }

            // 更新图标为灰色
            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = CreateGrayIcon(GetApplicationIcon());
                _notifyIcon.Text = "HyperMark - 服务已停止";
            }

            ShowNotification("服务已暂停", "HTTP 服务已停止，可通过托盘菜单重新启动");
        }
        else
        {
            // 恢复服务
            await _app.StartAsync();
            _isRunning = true;

            // 更新 UI
            if (_toggleItem != null) _toggleItem.Text = "暂停服务";
            if (_statusItem != null)
            {
                _statusItem.Text = "✓ 服务运行中";
                _statusItem.ForeColor = Color.Green;
            }

            // 恢复正常图标
            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = GetApplicationIcon();
                _notifyIcon.Text = "HyperMark - 网页收藏服务";
            }

            ShowNotification("服务已恢复", "HTTP 服务已启动");
        }
    }

    /// <summary>
    /// 创建灰色图标
    /// </summary>
    private Icon CreateGrayIcon(Icon originalIcon)
    {
        using var bitmap = originalIcon.ToBitmap();
        var grayBitmap = new Bitmap(bitmap.Width, bitmap.Height);

        for (int x = 0; x < bitmap.Width; x++)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var gray = (int)(pixel.R * 0.3 + pixel.G * 0.59 + pixel.B * 0.11);
                grayBitmap.SetPixel(x, y, Color.FromArgb(pixel.A, gray, gray, gray));
            }
        }

        return Icon.FromHandle(grayBitmap.GetHicon());
    }

    /// <summary>
    /// 打开 Web 界面
    /// </summary>
    private void OpenWebInterface()
    {
        try
        {
            var config = HyperMark.Config.AppConfig.Instance;
            var url = config.Http;

            // 如果服务已停止，提示用户
            if (!_isRunning)
            {
                ShowNotification("服务已停止", "请先启动服务后再打开 Web 界面");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowNotification("打开失败", $"无法打开浏览器: {ex.Message}");
        }
    }

    /// <summary>
    /// 打开数据目录
    /// </summary>
    private void OpenDataDirectory()
    {
        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hypermark"
            );

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = dataDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowNotification("打开失败", $"无法打开数据目录: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换开机自动启动
    /// </summary>
    private void ToggleAutoStart()
    {
        var currentState = StartupManager.IsAutoStartEnabled();
        StartupManager.SetAutoStart(!currentState);

        // 更新菜单项状态
        if (_autoStartItem != null)
        {
            _autoStartItem.Checked = !currentState;
        }

        ShowNotification(
            "设置已更新",
            !currentState ? "已启用开机自动启动" : "已禁用开机自动启动"
        );
    }

    /// <summary>
    /// 退出应用
    /// </summary>
    private async Task ExitApplication()
    {
        // 显示确认对话框（可选）
        var result = MessageBox.Show(
            "确定要退出 HyperMark 吗？",
            "退出确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (result == DialogResult.Yes)
        {
            // 停止服务
            if (_isRunning)
            {
                await _app.StopAsync();
            }

            // 清理托盘图标
            Dispose();

            // 退出应用
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// 显示气泡通知
    /// </summary>
    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
            _disposed = true;
        }
    }
}
