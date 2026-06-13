using System.Text.Json;
using System.Text.Json.Serialization;

namespace HyperMark.Config;

/// <summary>
/// 应用配置，从 .hypermark/config.json 加载
/// </summary>
public class AppConfig
{
    /// <summary>
    /// HTTP 监听地址，默认 http://localhost:5000
    /// </summary>
    [JsonPropertyName("http")]
    public string Http { get; set; } = "http://localhost:5000";

    /// <summary>
    /// 是否开机自动启动，默认 false
    /// </summary>
    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// 是否最小化启动（启动后最小化到托盘），默认 false
    /// </summary>
    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; } = false;

    private static AppConfig? _instance;

    /// <summary>
    /// 数据目录路径
    /// </summary>
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".hypermark"
    );

    /// <summary>
    /// 获取配置单例（懒加载）
    /// </summary>
    public static AppConfig Instance => _instance ??= Load();

    /// <summary>
    /// 启动时初始化：检测并创建数据目录和默认配置文件
    /// </summary>
    public static void EnsureInitialized()
    {
        // 创建数据目录及子目录
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(Path.Combine(DataDir, "sites"));
        Directory.CreateDirectory(Path.Combine(DataDir, "bookmarks"));

        // 创建默认配置文件
        var configPath = Path.Combine(DataDir, "config.json");
        if (!File.Exists(configPath))
        {
            var defaultConfig = new AppConfig();
            var json = JsonSerializer.Serialize(defaultConfig, HyperMarkJsonContext.Default.AppConfig);
            File.WriteAllText(configPath, json + Environment.NewLine);
        }
    }

    /// <summary>
    /// 从用户目录加载配置文件
    /// </summary>
    private static AppConfig Load()
    {
        var configPath = Path.Combine(DataDir, "config.json");

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.AppConfig);
                if (config != null)
                {
                    return config;
                }
            }
            catch
            {
                // 配置文件格式错误时返回默认配置
            }
        }

        return new AppConfig();
    }
}
