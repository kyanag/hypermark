namespace HyperMark.Models;

/// <summary>
/// 域名与站点映射关系
/// </summary>
public class SiteDomain
{
    /// <summary>
    /// 域名（如 "github.com"）
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// 所属站点名称
    /// </summary>
    public string SiteName { get; set; } = string.Empty;
}
