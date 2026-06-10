using HyperMark.Matching;
using HyperMark.Models;
using HyperMark.Storage;

namespace HyperMark.Parsers;

/// <summary>
/// URL 解析器
/// 根据站点元信息和路由规则，将 URL 解析为 Page 元信息
/// </summary>
public class UrlParser
{
    private readonly Repository _repository;

    public UrlParser(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 解析 URL 为 Page 元信息
    /// </summary>
    /// <param name="url">要解析的 URL</param>
    /// <returns>解析后的 Page 对象</returns>
    public Page Parse(string url)
    {
        // 1. 解析 URL，获取各组成部分
        var uri = new Uri(url);
        var domain = uri.Host;
        var path = uri.PathAndQuery;

        // 2. 优先通过 Site.Domains 匹配站点
        var allSites = _repository.GetAllSites();
        var domainMatchedSites = allSites.Where(s =>
            s.Domains.Contains(domain)
        ).ToList();

        // 3. 若 Site.Domains 未匹配，查询 domain_cnames 表获取真实域名，再通过真实域名匹配站点
        if (domainMatchedSites.Count == 0)
        {
            var cname = _repository.GetCnameByDomain(domain);
            if (!string.IsNullOrEmpty(cname))
            {
                domainMatchedSites = allSites.Where(s =>
                    s.Domains.Contains(cname)
                ).ToList();
            }
        }

        // 4. 根据 homepage 进行最终匹配，如果没有匹配到则使用兜底站点
        Site matchedSite;
        if (domainMatchedSites.Count == 0)
        {
            matchedSite = Fallback.DefaultSite;
        }
        else
        {
            matchedSite = FindSiteByHomepage(domainMatchedSites, uri) ?? Fallback.DefaultSite;
        }

        // 4. 找到匹配的路由，如果没有匹配到则使用兜底路由
        var routeResult = FindMatchingRoute(matchedSite, path);
        Route matchedRoute;
        Dictionary<string, object> routeArgs;

        if (routeResult == null)
        {
            // 使用兜底路由，捕获完整路径
            matchedRoute = Fallback.DefaultRoute;
            routeArgs = new Dictionary<string, object>
            {
                ["path"] = path.TrimStart('/')
            };
        }
        else
        {
            (matchedRoute, routeArgs) = routeResult.Value;
        }

        // 5. 根据路由解析 URL，得到 Page
        return BuildPage(matchedSite, matchedRoute, uri, path, routeArgs);
    }

    /// <summary>
    /// 根据 homepage 匹配站点
    /// </summary>
    private Site? FindSiteByHomepage(List<Site> sites, Uri uri)
    {
        var urlPath = uri.AbsolutePath;

        foreach (var site in sites)
        {
            // 兜底站点特殊处理（Homepage 为空）
            if (string.IsNullOrEmpty(site.Homepage))
            {
                continue;
            }

            var homepageUri = new Uri(site.Homepage);
            var homepagePath = homepageUri.AbsolutePath;

            // homepage 为空或只有/
            if (string.IsNullOrEmpty(homepagePath) || homepagePath == "/")
            {
                return site;
            }

            // 移除末尾斜杠进行比较
            var homepagePathNormalized = homepagePath.TrimEnd('/');
            var urlPathNormalized = urlPath.TrimEnd('/');

            // URL path 以 homepage path 开头
            if (urlPathNormalized.StartsWith(homepagePathNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return site;
            }
        }

        return null;
    }

    /// <summary>
    /// 查找匹配的路由
    /// </summary>
    private (Route Route, Dictionary<string, object> Args)? FindMatchingRoute(Site site, string fullPath)
    {
        // 兜底站点（Homepage 为空）直接返回 null，由调用方处理
        if (string.IsNullOrEmpty(site.Homepage))
        {
            return null;
        }

        var uri = new Uri(fullPath.Contains("://") ? fullPath : "https://placeholder.com" + fullPath);
        var relativePath = GetRelativePath(site.Homepage, uri.AbsolutePath);

        // 构建相对的完整路径（包含 query）
        var relativePathWithQuery = string.IsNullOrEmpty(uri.Query)
            ? relativePath
            : relativePath + uri.Query;

        foreach (var route in site.Routes)
        {
            var args = RouteMatcher.Match(route, relativePathWithQuery);

            if (args != null)
            {
                // 检查必填参数
                if (IsRequiredParamsMissing(args, route.Args))
                {
                    continue;
                }

                return (route, args);
            }
        }

        return null;
    }

    /// <summary>
    /// 获取相对于 homepage 的路径
    /// </summary>
    private string GetRelativePath(string homepage, string fullPath)
    {
        try
        {
            var homepageUri = new Uri(homepage);
            var homepagePath = homepageUri.AbsolutePath;

            if (string.IsNullOrEmpty(homepagePath) || homepagePath == "/")
            {
                return fullPath;
            }

            // 去掉 homepage path 前缀
            if (fullPath.StartsWith(homepagePath, StringComparison.OrdinalIgnoreCase))
            {
                var relative = fullPath[homepagePath.Length..];
                return string.IsNullOrEmpty(relative) ? "/" : relative;
            }

            return fullPath;
        }
        catch
        {
            return fullPath;
        }
    }

    /// <summary>
    /// 合并 URL 内置参数到新字典，routeArgs 优先（同名参数不被覆盖）
    /// 不修改原始 args 字典，避免污染 Page.Args
    /// </summary>
    private static Dictionary<string, object> MergeBuiltInUrlArgs(Uri uri, Dictionary<string, object> args)
    {
        var merged = new Dictionary<string, object>
        {
            ["_uri"]            = uri.ToString(),                          // 域名
            ["_target"]         = uri.PathAndQuery + uri.Fragment,  // 完整请求路径（不含 scheme://host）
            ["_pathname"]       = uri.AbsolutePath,                  // 路径部分（不含 query 和 fragment）
            ["_filename"]       = uri.Segments.Length > 0 ? uri.Segments[^1] : string.Empty,  // 文件名（最后一段路径）
            ["_fragment"]       = uri.IsAbsoluteUri ? uri.Fragment.TrimStart('#') : string.Empty, // 锚点（不含 #）
            ["_domain"]         = uri.Host,                          // 域名
        };

        // routeArgs 优先，覆盖同名内置参数
        foreach (var kvp in args)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    /// <summary>
    /// 检查是否缺少必需的参数
    /// </summary>
    private bool IsRequiredParamsMissing(Dictionary<string, object> args, List<RouteArg> patternArgs)
    {
        foreach (var arg in patternArgs.Where(a => a.IsRequired))
        {
            if (!args.ContainsKey(arg.Name))
            {
                return true;
            }

            var value = args[arg.Name]?.ToString();
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 构建 Page 对象
    /// </summary>
    private Page BuildPage(Site site, Route route, Uri uri, string relativePath, Dictionary<string, object> args)
    {
        // 应用默认值
        foreach (var arg in route.Args.Where(a => a.Default != null))
        {
            if (!args.ContainsKey(arg.Name))
            {
                args[arg.Name] = arg.Default!;
            }
        }

        // 构建标准 URL：合并内置参数到新字典，不污染 Page.Args
        var stdArgs = MergeBuiltInUrlArgs(uri, args);
        var stdPath = RouteMatcher.BuildStdUrl(route, stdArgs);
        var stdUrl = $"{uri.Scheme}://{site.Name}{stdPath}";

        // 转换为 Dictionary<string, string>
        var stringArgs = args.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);

        return new Page
        {
            Original = uri.ToString(),
            Std = stdUrl,
            Args = stringArgs,
            Tags = route.Tags.ToList(),
            CreateAt = DateTime.Now,
            Site = site.Name,
            Route = route.Name,
            HyperId = $"{site.Name}://{stdUrl}"
        };
    }
}
