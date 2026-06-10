namespace HyperMark.Models;

/// <summary>
/// Page 元信息模型
/// 链接被解析后生成的标准格式化的信息
/// </summary>
public class Page
{
    /// <summary>
    /// 原始链接
    /// </summary>
    public string Original { get; set; } = string.Empty;

    /// <summary>
    /// 标准化链接
    /// </summary>
    public string Std { get; set; } = string.Empty;

    /// <summary>
    /// 解析参数
    /// </summary>
    public Dictionary<string, string> Args { get; set; } = new();
    
    /// <summary>
    /// 解析后的标签
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 添加时间
    /// </summary>
    public DateTime CreateAt { get; set; }

    /// <summary>
    /// 所属站点
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// 路由分支
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// 标准 ID (格式：site://std_url)
    /// </summary>
    public string HyperId { get; set; } = string.Empty;
}
