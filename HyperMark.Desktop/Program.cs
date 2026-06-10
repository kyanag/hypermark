using System.Diagnostics;
using HyperMark.Config;
using HyperMark.Web;
using Microsoft.AspNetCore.Builder;

namespace HyperMark.Desktop;

internal static class Program
{
    private static WebApplication? _app;
    private static TrayIcon? _trayIcon;

    [STAThread]
    public static void Main(string[] args)
    {
        // 检查是否最小化启动（开机自启动时使用）
        var startMinimized = args.Contains("--minimized");

        // 检查是否已有实例在运行
        var currentProcess = Process.GetCurrentProcess();
        var runningProcess = Process.GetProcessesByName(currentProcess.ProcessName)
            .FirstOrDefault(p => p.Id != currentProcess.Id);

        if (runningProcess != null)
        {
            MessageBox.Show(
                "HyperMark 已经在运行中，请检查系统托盘。",
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        // 初始化 WinForms
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // 初始化配置
        AppConfig.EnsureInitialized();
        var config = AppConfig.Instance;

        try
        {
            // 创建 Web 服务
            _app = WebHost.CreateApp(args);

            // 初始化托盘图标
            _trayIcon = new TrayIcon(_app);

            // 启动 Web 服务（非阻塞）
            _app.StartAsync().GetAwaiter().GetResult();

            // 显示启动通知
            if (!startMinimized)
            {
                _trayIcon.ShowNotification(
                    "HyperMark 已启动",
                    $"HTTP 服务运行于 {config.Http}"
                );
            }

            // 运行 WinForms 消息循环（阻塞，处理托盘图标事件）
            Application.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"启动失败: {ex.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        finally
        {
            // 清理
            _trayIcon?.Dispose();
            _app?.StopAsync().GetAwaiter().GetResult();
        }
    }
}
