namespace HyperMark.Models;

/// <summary>
/// 分类模型
/// 使用 Name 作为唯一标识，通过 ParentId 指向父分类的 Name 实现层级关系
/// </summary>
public class Category
{
    /// <summary>
    /// 分类名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 父分类名称，null 表示根分类
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
