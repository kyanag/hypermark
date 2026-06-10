namespace HyperMark.Models;

/// <summary>
/// 查询参数解析模式
/// </summary>
public enum QueryParseMode
{
    /// <summary>
    /// 标准查询参数：?key=value&key2=value2
    /// </summary>
    Standard,

    /// <summary>
    /// 路径风格：? 后部分整体当作路径，直接用正则匹配
    /// 支持如 ?tid-{tid}-keyword-{keyword}.html 这类格式
    /// </summary>
    PathStyle
}

/// <summary>
/// 路由模型
/// </summary>
public class Route
{
    /// <summary>
    /// 路由标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 路由名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL 模式（如 /acting/{category_id} 或 /forum.php?mod=viewthread&tid={thread_id}）
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// 路由参数说明
    /// </summary>
    public List<RouteArg> Args { get; set; } = new();

    /// <summary>
    /// 标准化 URL 格式
    /// </summary>
    public string StdFormat { get; set; } = string.Empty;

    /// <summary>
    /// 分类标签
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 查询参数解析模式（null 则自动推断）
    /// </summary>
    public QueryParseMode? QueryMode { get; set; }
}
