using System.Diagnostics;
using Avalonia;
using HyperMark;
using HyperMark.Config;
using HyperMark.Web;
using Microsoft.AspNetCore.Builder;

namespace HyperMark.Desktop;

internal static class Program
{
    internal static WebApplication? App;
    private static FileLogger? _fileLogger;

    [STAThread]
    public static void Main(string[] args)
    {
        // 初始化日志记录器（尽早初始化以捕获启动异常）
        _fileLogger = new FileLogger();

        // 注册全局异常处理
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 检查是否已有实例在运行
#if !DEBUG
        var currentProcess = Process.GetCurrentProcess();
        var runningProcess = Process.GetProcessesByName(currentProcess.ProcessName)
            .FirstOrDefault(p => p.Id != currentProcess.Id);

        if (runningProcess != null)
        {
            Console.Error.WriteLine("HyperMark 已经在运行中，请检查系统托盘。");
            _fileLogger.LogError("HyperMark 已经在运行中，请检查系统托盘。");
            return;
        }
#endif

        // 初始化配置
        AppConfig.EnsureInitialized();

#if DEBUG
        // Debug 模式下使用 5001 端口，避免与 Release 版本冲突
        AppConfig.Instance.Http = "http://localhost:5001";
#endif

        _fileLogger.LogError("HyperMark Desktop 启动中...");

        try
        {
            // 创建 Web 服务（端口由 config.json 中的 http 字段控制）
            App = WebHost.CreateApp(args);
            App.StartAsync().GetAwaiter().GetResult();

            _fileLogger.LogError("Web 服务启动成功");

            // 启动 Avalonia（含托盘图标，阻塞直到退出）
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            _fileLogger.LogFatal("启动失败", ex);
            Console.Error.WriteLine($"启动失败: {ex.Message}");
        }
        finally
        {
            try
            {
                App?.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("停止 Web 服务失败", ex);
                Console.Error.WriteLine($"停止 Web 服务失败: {ex.Message}");
            }
            _fileLogger.LogError("HyperMark Desktop 已停止");
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _fileLogger?.LogFatal("未处理的异常", ex);
        }
        else
        {
            _fileLogger?.LogFatal($"未处理的异常对象: {e.ExceptionObject}");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _fileLogger?.LogError("未观察到的 Task 异常", e.Exception);
        e.SetObserved(); // 防止进程终止
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
