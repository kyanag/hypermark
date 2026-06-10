namespace HyperMark.Models;

/// <summary>
/// 路由参数模型
/// </summary>
public class RouteArg
{
    /// <summary>
    /// 参数标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 字段名称 (pattern 中占位)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 默认值
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// 是否必须
    /// 规则设定为 true 时，如果 URL 没有解析出当前值即为不合法 URL
    /// </summary>
    public bool IsRequired { get; set; }
}
