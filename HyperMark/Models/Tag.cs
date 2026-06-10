namespace HyperMark.Models;

/// <summary>
/// 标签模型
/// </summary>
public class Tag
{
    /// <summary>
    /// 标签 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 标签名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 标签标题（显示用）
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
