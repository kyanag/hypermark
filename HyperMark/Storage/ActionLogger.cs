using System.Text.Json;
using HyperMark.Models;

namespace HyperMark.Storage;

/// <summary>
/// 操作日志装饰器
/// 包装任意 IStorage，将写操作记录到 actions 文件，用于数据恢复
/// </summary>
public class ActionLogger : IStorage
{
    private readonly IStorage _inner;
    private readonly string _siteLogPath;
    private readonly string _linkLogPath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = HyperMarkJsonContext.Instance.Options;

    public ActionLogger(IStorage inner, string? basePath = null)
    {
        _inner = inner;
        basePath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hypermark");
        Directory.CreateDirectory(basePath);
        _siteLogPath = Path.Combine(basePath, "sites.actions");
        _linkLogPath = Path.Combine(basePath, "links.actions");
    }

    public bool AddSite(Site site)
    {
        var result = _inner.AddSite(site);
        if (result)
        {
            Log(_siteLogPath, "add_site", $"{{\"Name\":\"{EscapeJson(site.Name)}\"}}");
        }
        return result;
    }

    public bool DeleteSite(string siteName)
    {
        var result = _inner.DeleteSite(siteName);
        if (result)
        {
            Log(_siteLogPath, "delete_site", $"{{\"SiteName\":\"{EscapeJson(siteName)}\"}}");
        }
        return result;
    }

    public bool ExistsSite(Site site) => _inner.ExistsSite(site);
    public MarkStatus IsMarked(string uri) => _inner.IsMarked(uri);
    public List<Site> Sites() => _inner.Sites();
    public Site? GetSite(string siteName) => _inner.GetSite(siteName);
    public List<Link> GetLinksBySite(string siteName) => _inner.GetLinksBySite(siteName);

    public Link? GetLinkByUrl(string url) => _inner.GetLinkByUrl(url);
    public Link? GetLinkByHyperId(string hyperId) => _inner.GetLinkByHyperId(hyperId);

    public bool AddLink(Link link)
    {
        var result = _inner.AddLink(link);
        if (result)
        {
            Log(_linkLogPath, "add_link", $"{{\"Url\":\"{EscapeJson(link.Url)}\",\"Title\":\"{EscapeJson(link.Title)}\",\"CreatedAt\":\"{link.CreatedAt:O}\",\"Category\":\"{EscapeJson(link.Category)}\"}}");
        }
        return result;
    }

    public bool UpdateLinkCategory(string url, string category) => _inner.UpdateLinkCategory(url, category);
    public bool UpdateLinkTitle(string url, string title) => _inner.UpdateLinkTitle(url, title);
    public bool UpdateLinkPage(string url, Page page) => _inner.UpdateLinkPage(url, page);
    public bool UpdateLinkTags(string url, List<string> tags) => _inner.UpdateLinkTags(url, tags);
    public bool UpdateLinkValues(string url, Dictionary<string, object>? values) => _inner.UpdateLinkValues(url, values);
    public List<Link> GetLinks(string? category = null) => _inner.GetLinks(category);

    public bool DeleteLink(string url)
    {
        var result = _inner.DeleteLink(url);
        if (result)
        {
            Log(_linkLogPath, "delete_link", $"{{\"Url\":\"{EscapeJson(url)}\"}}");
        }
        return result;
    }

    public bool DeleteLinkByHyperId(string hyperId)
    {
        var result = _inner.DeleteLinkByHyperId(hyperId);
        if (result)
        {
            Log(_linkLogPath, "delete_link_by_hyperid", $"{{\"HyperId\":\"{EscapeJson(hyperId)}\"}}");
        }
        return result;
    }

    public List<Category> GetCategories() => _inner.GetCategories();
    public Category? GetCategory(string name) => _inner.GetCategory(name);
    public string AddCategory(Category category) => _inner.AddCategory(category);
    public bool DeleteCategory(string name) => _inner.DeleteCategory(name);
    public bool UpdateCategoryName(string oldName, string newName) => _inner.UpdateCategoryName(oldName, newName);
    public bool MoveCategory(string name, string? newParentName) => _inner.MoveCategory(name, newParentName);

    #region 标签操作

    public List<Tag> GetTags() => _inner.GetTags();
    public Tag? GetTag(int id) => _inner.GetTag(id);
    public Tag? GetTagByName(string name) => _inner.GetTagByName(name);
    public int AddTag(Tag tag) => _inner.AddTag(tag);
    public bool DeleteTag(int id) => _inner.DeleteTag(id);
    public bool UpdateTagName(int id, string name) => _inner.UpdateTagName(id, name);
    public bool UpdateTagTitle(int id, string title) => _inner.UpdateTagTitle(id, title);
    public List<Link> GetLinksByTag(int tagId) => _inner.GetLinksByTag(tagId);

    #endregion

    public bool AddDomainCname(string domain, string cname) => _inner.AddDomainCname(domain, cname);
    public bool RemoveDomainCname(string domain) => _inner.RemoveDomainCname(domain);
    public string? GetCnameByDomain(string domain) => _inner.GetCnameByDomain(domain);
    public List<string> GetDomainsByCname(string cname) => _inner.GetDomainsByCname(cname);
    public List<(string Domain, string Cname)> GetAllDomainCnames() => _inner.GetAllDomainCnames();
    public bool RemoveAllCnames() => _inner.RemoveAllCnames();

    /// <summary>
    /// 从 links.actions 日志文件重建链接数据
    /// 清空当前链接数据，按日志顺序重放
    /// </summary>
    public int ReplayLinks()
    {
        if (!File.Exists(_linkLogPath))
        {
            return 0;
        }

        var lines = File.ReadAllLines(_linkLogPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        if (lines.Length == 0)
        {
            return 0;
        }

        var linkStates = new Dictionary<string, LinkState>();

        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var action = root.GetProperty("action").GetString()!;
            var payload = root.GetProperty("payload");

            switch (action)
            {
                case "add_link":
                {
                    var url = payload.GetProperty("Url").GetString()!;
                    var state = new LinkState
                    {
                        Url = url,
                        Title = payload.TryGetProperty("Title", out var t) ? t.GetString()! : url,
                        CreatedAt = payload.TryGetProperty("CreatedAt", out var c) ? DateTime.Parse(c.GetString()!) : DateTime.Now,
                        Category = payload.TryGetProperty("Category", out var cat) && cat.ValueKind != JsonValueKind.Null ? cat.GetString()! : string.Empty,
                        Deleted = false
                    };
                    linkStates[url] = state;
                    break;
                }
                case "delete_link":
                {
                    var url = payload.GetProperty("Url").GetString()!;
                    if (linkStates.TryGetValue(url, out var state))
                    {
                        state.Deleted = true;
                    }
                    break;
                }
            }
        }

        var linksToRestore = linkStates.Values.Where(s => !s.Deleted).ToList();
        return RestoreLinks(linksToRestore);
    }

    private int RestoreLinks(List<LinkState> linkStates)
    {
        // 先删除所有现有链接
        foreach (var state in linkStates)
        {
            _inner.DeleteLink(state.Url);
        }

        // 逐个重新添加
        var count = 0;
        foreach (var state in linkStates)
        {
            var link = new Link
            {
                Url = state.Url,
                Title = state.Title,
                CreatedAt = state.CreatedAt,
                Category = state.Category
            };
            if (_inner.AddLink(link))
            {
                count++;
            }
        }
        return count;
    }

    private class LinkState
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool Deleted { get; set; }
    }

    private void Log(string logPath, string action, string payloadJson)
    {
        lock (_lock)
        {
            var line = $"{{\"action\":\"{action}\",\"timestamp\":\"{DateTime.Now:O}\",\"payload\":{payloadJson}}}";
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
