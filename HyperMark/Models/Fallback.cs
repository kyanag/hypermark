namespace HyperMark.Models;

/// <summary>
/// 兜底站点和路由定义
/// </summary>
public static class Fallback
{
    /// <summary>
    /// 默认兜底站点
    /// </summary>
    public static Site DefaultSite => new()
    {
        Title = "[其他]",
        Name = "other",
        Homepage = "",
        Domains = new List<string>(),
        Routes = new List<Route> { DefaultRoute },
        Vars = new Dictionary<string, string>()
    };

    /// <summary>
    /// 默认兜底路由（匹配所有路径）
    /// </summary>
    public static Route DefaultRoute => new()
    {
        Title = "其他",
        Name = "other",
        Pattern = "(.*)",
        Args = new List<RouteArg>
        {},
        StdFormat = "{_target}",
        Tags = new List<string> { "@other" }
    };

    /// <summary>
    /// 判断是否为兜底站点
    /// </summary>
    public static bool IsFallbackSite(string siteName) => siteName == "other";

    /// <summary>
    /// 判断是否为兜底路由
    /// </summary>
    public static bool IsFallbackRoute(string routeName) => routeName == "other";
}