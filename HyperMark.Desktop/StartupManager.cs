using Microsoft.Win32;
using HyperMark.Config;

namespace HyperMark.Desktop;

/// <summary>
/// 开机启动管理器
/// </summary>
public static class StartupManager
{
    private const string AppName = "HyperMark";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// 检查是否已启用开机自动启动
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 设置开机自动启动
    /// </summary>
    /// <param name="enable">是否启用</param>
    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    // 添加 --minimized 参数，启动时最小化到托盘
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            // 同步到配置文件
            UpdateConfig(enable);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"设置开机启动失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新配置文件中的自动启动设置
    /// </summary>
    private static void UpdateConfig(bool autoStart)
    {
        try
        {
            var configPath = Path.Combine(HyperMark.Config.AppConfig.DataDir, "config.json");
            if (!File.Exists(configPath)) return;

            var json = File.ReadAllText(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize(
                json,
                HyperMark.HyperMarkJsonContext.Instance.AppConfig
            );

            if (config != null)
            {
                config.AutoStart = autoStart;
                var newJson = System.Text.Json.JsonSerializer.Serialize(
                    config,
                    HyperMark.HyperMarkJsonContext.Instance.AppConfig
                );
                File.WriteAllText(configPath, newJson + Environment.NewLine);
            }
        }
        catch
        {
            // 配置更新失败不影响主要功能
        }
    }
}
