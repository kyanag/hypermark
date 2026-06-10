namespace HyperMark.Models;

/// <summary>
/// 内置标签定义
/// </summary>
public static class BuiltInTags
{
    /// <summary>
    /// 已解析 — 表示 Link 的 Page 信息已被解析
    /// </summary>
    public static class PageParsed
    {
        public const string Name = "system:page";
        public const string Title = "已解析";
    }

    /// <summary>
    /// 无站点 — 表示 Link 的 Page 不属于已有站点（site == "other"）
    /// </summary>
    public static class NoSite
    {
        public const string Name = "system:nosite";
        public const string Title = "无站点";
    }

    /// <summary>
    /// 所有内置标签的名称
    /// </summary>
    public static readonly IReadOnlyList<string> Names =
    [
        PageParsed.Name,
        NoSite.Name
    ];

    /// <summary>
    /// 根据 Link 的 Page 信息确定应包含的内置标签名称
    /// </summary>
    public static List<string> ResolveForLink(Link link)
    {
        var tags = new List<string>();
        if (link.Page != null && !string.IsNullOrEmpty(link.Page.HyperId))
        {
            tags.Add(PageParsed.Name);
        }
        if (link.Page != null && link.Page.Site == "other")
        {
            tags.Add(NoSite.Name);
        }
        return tags;
    }
}
