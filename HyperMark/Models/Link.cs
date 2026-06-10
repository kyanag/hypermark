
namespace HyperMark.Models;

/// <summary>
/// 链接收藏模型
/// </summary>
public class Link
{
    /// <summary>
    /// 链接名称（主键）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 链接地址
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 链接标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 收藏时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 所属分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 解析后的 Page 元信息
    /// </summary>
    public Page? Page { get; set; }

    /// <summary>
    /// 标签名称列表
    /// </summary>
    public List<string> Tags { get; set; } = [];

}
