namespace HyperMark.Models;

/// <summary>
/// 站点元信息模型
/// </summary>
public class Site
{
    /// <summary>
    /// 站点名称 (标题), 显示用
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 站点 id, 具有唯一性
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 主页 URL, 可以是根域名或二级三级目录
    /// </summary>
    public string Homepage { get; set; } = string.Empty;

    /// <summary>
    /// 同站点其他域名列表
    /// </summary>
    public List<string> Domains { get; set; } = new();

    /// <summary>
    /// 路由规则数组
    /// </summary>
    public List<Route> Routes { get; set; } = new();

    /// <summary>
    /// 站点其他属性
    /// </summary>
    public Dictionary<string, string> Vars { get; set; } = new();
}
