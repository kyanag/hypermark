namespace HyperMark;

/// <summary>
/// 简单的文件日志工具
/// 访问日志和错误日志分别写入不同文件
/// </summary>
public class FileLogger
{
    private readonly string _accessLogPath;
    private readonly string _errorLogPath;
    private readonly object _lock = new();

    public FileLogger(string? basePath = null)
    {
        // 默认放在项目目录下的 logs 目录，不污染用户数据目录
        basePath ??= Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(basePath);
        _accessLogPath = Path.Combine(basePath, "_access.log");
        _errorLogPath = Path.Combine(basePath, "_error.log");
    }

    /// <summary>
    /// 记录访问日志
    /// 格式: [时间] 方法 路径 状态码 耗时ms
    /// </summary>
    public void LogAccess(string method, string path, int statusCode, long elapsedMs)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {method} {path} {statusCode} {elapsedMs}ms";
        AppendToFile(_accessLogPath, line);
    }

    /// <summary>
    /// 记录错误日志
    /// 格式: [时间] 错误信息
    /// </summary>
    public void LogError(string message, Exception? ex = null)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        if (ex != null)
        {
            line += Environment.NewLine + ex.ToString();
        }
        AppendToFile(_errorLogPath, line);
    }

    /// <summary>
    /// 记录错误日志（简化方法）
    /// </summary>
    public void LogError(Exception ex, string? context = null)
    {
        var message = context != null ? $"{context}: {ex.Message}" : ex.Message;
        LogError(message, ex);
    }

    private void AppendToFile(string path, string content)
    {
        lock (_lock)
        {
            try
            {
                File.AppendAllText(path, content + Environment.NewLine);
            }
            catch
            {
                // 日志写入失败不能再抛异常，避免循环
            }
        }
    }
}
