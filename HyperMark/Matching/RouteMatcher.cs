using System.Text.RegularExpressions;
using HyperMark.Models;

namespace HyperMark.Matching;

/// <summary>
/// 路由匹配器
/// 根据 Route 定义匹配 URL 并解析参数
/// </summary>
public static class RouteMatcher
{
    /// <summary>
    /// 匹配路由并解析参数
    /// </summary>
    /// <param name="route">路由定义</param>
    /// <param name="url">要匹配的 URL（可以是完整 URL 或路径）</param>
    /// <returns>匹配成功返回参数字典，失败返回 null</returns>
    public static Dictionary<string, object>? Match(Route route, string url)
    {
        // 解析 URL 获取路径和 query
        string path;
        string? rawQueryString = null;
        Dictionary<string, string> queryParams;
        try
        {
            if (url.Contains("://"))
            {
                var uri = new Uri(url);
                path = uri.AbsolutePath;
                rawQueryString = uri.Query;
                queryParams = ParseQueryString(rawQueryString);
            }
            else
            {
                // 假设是路径部分，可能包含 query
                var parts = url.Split('?', 2);
                path = parts[0].Split('#')[0];
                rawQueryString = parts.Length > 1 ? "?" + parts[1] : null;
                queryParams = rawQueryString != null ? ParseQueryString(rawQueryString) : new Dictionary<string, string>();
            }
        }
        catch
        {
            path = url;
            queryParams = new Dictionary<string, string>();
        }

        // 解析 pattern，分离 path 和 query 部分
        var patternParts = route.Pattern.Split('?', 2);
        var patternPath = patternParts[0];
        string? patternQuery = patternParts.Length > 1 ? patternParts[1] : null;

        // 判断解析模式
        var mode = route.QueryMode ?? InferQueryMode(patternQuery);

        // PathStyle 模式：把整个 URL（含 ? 后部分）当作路径匹配
        if (mode == QueryParseMode.PathStyle && !string.IsNullOrEmpty(patternQuery))
        {
            return MatchPathStyle(route, url, path, rawQueryString);
        }

        // Standard 模式：路径 + 查询参数分开匹配
        // 编译并匹配 path pattern
        var regex = CompilePattern(patternPath);
        var match = regex.Match(path);

        if (!match.Success)
        {
            return null;
        }

        // 提取 path 参数
        var args = new Dictionary<string, object>();
        foreach (var groupName in regex.GetGroupNames())
        {
            if (int.TryParse(groupName, out _))
            {
                continue; // 跳过数字分组
            }

            var value = match.Groups[groupName].Value;
            if (!string.IsNullOrEmpty(value))
            {
                args[groupName] = Uri.UnescapeDataString(value);
            }
        }

        // 如果 pattern 包含 query 部分，匹配并解析参数
        if (!string.IsNullOrEmpty(patternQuery))
        {
            var queryMatch = MatchQueryPattern(patternQuery, queryParams);
            if (queryMatch == null)
            {
                return null;
            }
            // 合并 query 解析的参数
            foreach (var kvp in queryMatch)
            {
                args[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            // 自动从 query 中补充 Args 中定义的参数
            foreach (var arg in route.Args)
            {
                if (!args.ContainsKey(arg.Name) && queryParams.TryGetValue(arg.Name, out var value))
                {
                    args[arg.Name] = value;
                }
            }
        }

        return args;
    }

    /// <summary>
    /// 从 pattern 中提取参数名（如 /article/{id} 提取出 id）
    /// </summary>
    private static HashSet<string> GetPatternParamNames(string pattern)
    {
        var paramNames = new HashSet<string>();
        var matches = Regex.Matches(pattern, @"\{([^}]+)\}");
        foreach (Match m in matches)
        {
            paramNames.Add(m.Groups[1].Value);
        }
        return paramNames;
    }

    /// <summary>
    /// 匹配 QueryPattern 并解析参数
    /// QueryPattern 格式示例：mod=viewthread&tid={thread_id}
    /// </summary>
    private static Dictionary<string, object>? MatchQueryPattern(string queryPattern, Dictionary<string, string> queryParams)
    {
        var args = new Dictionary<string, object>();

        // 解析 queryPattern 为键值对模式
        var patternPairs = queryPattern.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in patternPairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var patternValue = parts[1];

            // 检查是否是 {xxx} 占位符
            if (patternValue.StartsWith("{") && patternValue.EndsWith("}"))
            {
                var argName = patternValue[1..^1];
                if (queryParams.TryGetValue(key, out var value))
                {
                    args[argName] = value;
                }
                // 如果不是必填参数，允许不存在
            }
            else
            // 静态值匹配
            {
                if (!queryParams.TryGetValue(key, out var value) || value != patternValue)
                {
                    return null; // 静态值不匹配，整个路由不匹配
                }
            }
        }

        return args;
    }

    /// <summary>
    /// 推断查询参数解析模式
    /// </summary>
    private static QueryParseMode InferQueryMode(string? patternQuery)
    {
        if (string.IsNullOrEmpty(patternQuery))
        {
            return QueryParseMode.Standard;
        }

        // 如果包含 =，认为是标准格式
        // 否则认为是路径风格
        return patternQuery.Contains('=')
            ? QueryParseMode.Standard
            : QueryParseMode.PathStyle;
    }

    /// <summary>
    /// PathStyle 模式匹配：把整个 URL（含 ? 后部分）当作路径匹配
    /// </summary>
    private static Dictionary<string, object>? MatchPathStyle(Route route, string originalUrl, string path, string? rawQueryString)
    {
        // 构建完整路径：path + ? + query部分
        var fullPath = path;
        if (!string.IsNullOrEmpty(rawQueryString))
        {
            fullPath += rawQueryString; // rawQueryString 已包含 ?
        }

        // 编译 pattern 并匹配
        var regex = CompilePatternForPathStyle(route.Pattern);
        var match = regex.Match(fullPath);

        if (!match.Success)
        {
            return null;
        }

        // 提取参数
        var args = new Dictionary<string, object>();
        foreach (var groupName in regex.GetGroupNames())
        {
            if (int.TryParse(groupName, out _))
            {
                continue; // 跳过数字分组
            }

            var value = match.Groups[groupName].Value;
            if (!string.IsNullOrEmpty(value))
            {
                args[groupName] = Uri.UnescapeDataString(value);
            }
        }

        return args;
    }

    /// <summary>
    /// 为 PathStyle 模式编译正则表达式
    /// 与 CompilePattern 不同，这里允许匹配 ? 字符
    /// </summary>
    private static Regex CompilePatternForPathStyle(string pattern)
    {
        // 策略：用 {xxx} 占位符分割 pattern，转义各部分，再拼接
        var parts = Regex.Split(pattern, @"(\{[^}]+\})");
        var regexBuilder = new System.Text.StringBuilder("^");

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (part.StartsWith("{") && part.EndsWith("}"))
            {
                // 占位符：转为命名捕获组，使用非贪婪匹配
                var argName = part[1..^1];
                regexBuilder.Append($"(?<{argName}>.+?)");
            }
            else
            {
                // 静态部分：转义正则特殊字符
                regexBuilder.Append(Regex.Escape(part));
            }
        }

        regexBuilder.Append("$");

        return new Regex(regexBuilder.ToString(), RegexOptions.Compiled);
    }

    /// <summary>
    /// 解析查询字符串
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        var queryParams = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var queryParam in queryParams)
        {
            var parts = queryParam.Split('=', 2);
            if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
            {
                var key = parts[0];
                var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// 将 pattern 编译为正则表达式
    /// </summary>
    private static Regex CompilePattern(string pattern)
    {
        // 将 {xxx} 替换为命名捕获组 (?<xxx>[^/?#]+)
        var regexPattern = "^" + Regex.Replace(pattern, @"\{([^}]+)\}", "(?<${1}>[^/?#]+)") + "$";
        return new Regex(regexPattern, RegexOptions.Compiled);
    }

    /// <summary>
    /// 根据参数和路由定义构建标准化 URL
    /// </summary>
    public static string BuildStdUrl(Route route, Dictionary<string, object> args)
    {
        var result = route.StdFormat ?? string.Empty;
        foreach (var kvp in args)
        {
            result = result.Replace("{" + kvp.Key + "}", kvp.Value?.ToString() ?? string.Empty);
        }

        return result;
    }
}
