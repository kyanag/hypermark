using HyperMark.Models;

namespace HyperMark.Storage;

/// <summary>
/// 缓存代理层，包装任意 IStorage 实现，提供内存缓存
/// 写操作同时更新缓存和底层存储
/// </summary>
public class CacheStorage : IStorage
{
    private readonly IStorage _inner;
    private readonly object _lock = new();

    // 缓存
    private readonly Dictionary<string, Site> _sites = new();
    private readonly Dictionary<string, Category> _categories = new();
    private readonly Dictionary<int, Tag> _tags = new();
    private readonly Dictionary<string, string> _domainCnames = new();
    private readonly Dictionary<string, Link> _linksByUrl = new();
    private readonly Dictionary<string, Link> _linksByHyperId = new();
    private readonly Dictionary<string, List<Link>> _linksByCategory = new();
    private readonly Dictionary<string, List<Link>> _linksBySite = new();
    private readonly Dictionary<string, List<Link>> _linksByTag = new();

    private bool _loaded;

    public CacheStorage(IStorage inner)
    {
        _inner = inner;
    }

    #region 加载

    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            var sites = _inner.Sites();
            foreach (var s in sites) _sites[s.Name] = s;

            var cats = _inner.GetCategories();
            foreach (var c in cats) _categories[c.Name] = c;

            var tags = _inner.GetTags();
            foreach (var t in tags) _tags[t.Id] = t;

            var dcs = _inner.GetAllDomainCnames();
            foreach (var (d, c) in dcs) _domainCnames[d] = c;

            var allLinks = _inner.GetLinks();
            foreach (var link in allLinks) AddLinkToIndex(link);

            _loaded = true;
        }
    }

    private void AddLinkToIndex(Link link)
    {
        _linksByUrl[link.Url] = link;
        if (link.Page != null && !string.IsNullOrEmpty(link.Page.HyperId))
            _linksByHyperId[link.Page.HyperId] = link;
        if (!string.IsNullOrEmpty(link.Category))
            AddToGroup(_linksByCategory, link.Category, link);
        else
            AddToGroup(_linksByCategory, "", link);
        if (link.Page != null)
            AddToGroup(_linksBySite, link.Page.Site, link);
        foreach (var tag in link.Tags)
            AddToGroup(_linksByTag, tag, link);
    }

    private static void AddToGroup(Dictionary<string, List<Link>> dict, string key, Link link)
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = new List<Link>();
            dict[key] = list;
        }
        if (!list.Any(l => l.Url == link.Url))
            list.Add(link);
    }

    private static void RemoveLinkFromIndex(Dictionary<string, List<Link>> dict, string key, string url)
    {
        if (dict.TryGetValue(key, out var list))
            list.RemoveAll(l => l.Url == url);
    }

    private void InvalidateTagIndex(Link link)
    {
        foreach (var tag in link.Tags)
            RemoveLinkFromIndex(_linksByTag, tag, link.Url);
    }

    private static void SyncBuiltInTags(Link link)
    {
        var wanted = BuiltInTags.ResolveForLink(link);
        link.Tags.RemoveAll(t => BuiltInTags.Names.Contains(t) && !wanted.Contains(t));
        foreach (var tag in wanted)
        {
            if (!link.Tags.Contains(tag))
                link.Tags.Add(tag);
        }
    }

    #endregion

    #region 站点操作

    public bool AddSite(Site site)
    {
        if (!_inner.AddSite(site)) return false;
        lock (_lock) { _sites[site.Name] = site; }
        return true;
    }

    public bool ExistsSite(Site site)
    {
        EnsureLoaded();
        lock (_lock) return _sites.ContainsKey(site.Name);
    }

    public List<Site> Sites()
    {
        EnsureLoaded();
        lock (_lock) return _sites.Values.ToList();
    }

    public Site? GetSite(string siteName)
    {
        EnsureLoaded();
        lock (_lock) return _sites.TryGetValue(siteName, out var s) ? s : null;
    }

    public bool DeleteSite(string siteName)
    {
        if (!_inner.DeleteSite(siteName)) return false;
        lock (_lock)
        {
            _sites.Remove(siteName);
            // 清除该站点的链接缓存
            if (_linksBySite.TryGetValue(siteName, out var links))
            {
                foreach (var l in links)
                {
                    _linksByUrl.Remove(l.Url);
                    if (l.Page != null && !string.IsNullOrEmpty(l.Page.HyperId))
                        _linksByHyperId.Remove(l.Page.HyperId);
                    RemoveLinkFromIndex(_linksByCategory, l.Category, l.Url);
                    foreach (var t in l.Tags)
                        RemoveLinkFromIndex(_linksByTag, t, l.Url);
                }
                _linksBySite.Remove(siteName);
            }
        }
        return true;
    }

    #endregion

    #region 收录状态

    public MarkStatus IsMarked(string uri)
    {
        var parser = new Parsers.UrlParser(new Repository(this));
        var page = parser.Parse(uri);
        // 优先走缓存
        EnsureLoaded();
        lock (_lock)
        {
            foreach (var link in _linksByUrl.Values)
            {
                if (link.Page == null) continue;
                if (link.Page.Site == page.Site && link.Page.HyperId == page.HyperId)
                    return MarkStatus.Full;
            }
            foreach (var link in _linksByUrl.Values)
            {
                if (link.Page != null && link.Page.Site == page.Site && link.Page.Std == page.Std)
                    return MarkStatus.Half;
            }
        }
        return MarkStatus.No;
    }

    #endregion

    #region 链接操作

    public Link? GetLinkByUrl(string url)
    {
        EnsureLoaded();
        lock (_lock) return _linksByUrl.TryGetValue(url, out var l) ? l : null;
    }

    public Link? GetLinkByHyperId(string hyperId)
    {
        EnsureLoaded();
        lock (_lock) return _linksByHyperId.TryGetValue(hyperId, out var l) ? l : null;
    }

    public bool AddLink(Link link)
    {
        SyncBuiltInTags(link);
        if (!_inner.AddLink(link)) return false;
        lock (_lock) AddLinkToIndex(link);
        return true;
    }

    public bool UpdateLinkCategory(string url, string category)
    {
        if (!_inner.UpdateLinkCategory(url, category)) return false;
        lock (_lock)
        {
            if (!_linksByUrl.TryGetValue(url, out var link)) return false;
            var oldCat = link.Category;
            link.Category = category;
            RemoveLinkFromIndex(_linksByCategory, oldCat, url);
            AddToGroup(_linksByCategory, category, link);
            // 更新 _linksByUrl 中的引用
            _linksByUrl[url] = link;
        }
        return true;
    }

    public bool UpdateLinkTitle(string url, string title)
    {
        if (!_inner.UpdateLinkTitle(url, title)) return false;
        lock (_lock)
        {
            if (!_linksByUrl.TryGetValue(url, out var link)) return false;
            link.Title = title;
            _linksByUrl[url] = link;
        }
        return true;
    }

    public bool UpdateLinkPage(string url, Page page)
    {
        if (!_inner.UpdateLinkPage(url, page)) return false;
        lock (_lock)
        {
            if (!_linksByUrl.TryGetValue(url, out var link)) return false;
            // 移除旧 HyperId 索引
            if (link.Page != null && !string.IsNullOrEmpty(link.Page.HyperId))
                _linksByHyperId.Remove(link.Page.HyperId);
            link.Page = page;
            // 添加新 HyperId 索引
            if (!string.IsNullOrEmpty(page.HyperId))
                _linksByHyperId[page.HyperId] = link;
            SyncBuiltInTags(link);
            _linksByUrl[url] = link;
        }
        return true;
    }

    public bool UpdateLinkTags(string url, List<string> tags)
    {
        if (!_inner.UpdateLinkTags(url, tags)) return false;
        lock (_lock)
        {
            if (!_linksByUrl.TryGetValue(url, out var link)) return false;
            SyncBuiltInTags(link);
            tags.Clear();
            tags.AddRange(link.Tags);
            InvalidateTagIndex(link);
            foreach (var t in link.Tags)
                AddToGroup(_linksByTag, t, link);
            _linksByUrl[url] = link;
        }
        return true;
    }

    public bool UpdateLinkValues(string url, Dictionary<string, object>? values)
    {
        if (!_inner.UpdateLinkValues(url, values)) return false;
        lock (_lock)
        {
            if (!_linksByUrl.TryGetValue(url, out var link)) return false;
            link.Values = values;
            _linksByUrl[url] = link;
        }
        return true;
    }

    public List<Link> GetLinks(string? category = null)
    {
        EnsureLoaded();
        lock (_lock)
        {
            var key = category ?? "";
            if (_linksByCategory.TryGetValue(key, out var list))
                return list.OrderByDescending(l => l.CreatedAt).ToList();
            return new List<Link>();
        }
    }

    public List<Link> GetLinksBySite(string siteName)
    {
        EnsureLoaded();
        lock (_lock)
        {
            if (_linksBySite.TryGetValue(siteName, out var list))
                return list.OrderByDescending(l => l.CreatedAt).ToList();
            return new List<Link>();
        }
    }

    public bool DeleteLink(string url)
    {
        if (!_inner.DeleteLink(url)) return false;
        lock (_lock)
        {
            if (!_linksByUrl.TryGetValue(url, out var link)) return true;
            _linksByUrl.Remove(url);
            if (link.Page != null && !string.IsNullOrEmpty(link.Page.HyperId))
                _linksByHyperId.Remove(link.Page.HyperId);
            InvalidateTagIndex(link);
            RemoveLinkFromIndex(_linksByCategory, link.Category, url);
            if (link.Page != null)
                RemoveLinkFromIndex(_linksBySite, link.Page.Site, url);
        }
        return true;
    }

    public bool DeleteLinkByHyperId(string hyperId)
    {
        if (!_inner.DeleteLinkByHyperId(hyperId)) return false;
        lock (_lock)
        {
            if (!_linksByHyperId.TryGetValue(hyperId, out var link)) return true;
            _linksByHyperId.Remove(hyperId);
            _linksByUrl.Remove(link.Url);
            InvalidateTagIndex(link);
            RemoveLinkFromIndex(_linksByCategory, link.Category, link.Url);
            if (link.Page != null)
                RemoveLinkFromIndex(_linksBySite, link.Page.Site, link.Url);
        }
        return true;
    }

    #endregion

    #region 分类操作

    public List<Category> GetCategories()
    {
        EnsureLoaded();
        lock (_lock) return _categories.Values.ToList();
    }

    public Category? GetCategory(string name)
    {
        EnsureLoaded();
        lock (_lock) return _categories.TryGetValue(name, out var c) ? c : null;
    }

    public string AddCategory(Category category)
    {
        var result = _inner.AddCategory(category);
        if (string.IsNullOrEmpty(result) && _inner.GetCategory(category.Name) == null) return result;
        lock (_lock)
        {
            if (!_categories.ContainsKey(category.Name))
                _categories[category.Name] = category;
        }
        return result;
    }

    public bool MoveCategory(string name, string? newParentName)
    {
        if (!_inner.MoveCategory(name, newParentName)) return false;
        lock (_lock)
        {
            if (_categories.TryGetValue(name, out var cat))
                cat.ParentId = newParentName;
        }
        return true;
    }

    public bool DeleteCategory(string name)
    {
        if (!_inner.DeleteCategory(name)) return false;
        lock (_lock)
        {
            _categories.Remove(name);
            foreach (var cat in _categories.Values.Where(c => c.ParentId == name))
                cat.ParentId = null;
        }
        return true;
    }

    public bool UpdateCategoryName(string oldName, string newName)
    {
        if (!_inner.UpdateCategoryName(oldName, newName)) return false;
        lock (_lock)
        {
            if (!_categories.TryGetValue(oldName, out var cat)) return false;
            _categories.Remove(oldName);
            cat.Name = newName;
            _categories[newName] = cat;
            foreach (var c in _categories.Values.Where(c => c.ParentId == oldName))
                c.ParentId = newName;
        }
        return true;
    }

    #endregion

    #region 标签操作

    public List<Tag> GetTags()
    {
        EnsureLoaded();
        lock (_lock) return _tags.Values.ToList();
    }

    public Tag? GetTag(int id)
    {
        EnsureLoaded();
        lock (_lock) return _tags.TryGetValue(id, out var t) ? t : null;
    }

    public Tag? GetTagByName(string name)
    {
        EnsureLoaded();
        lock (_lock) return _tags.Values.FirstOrDefault(t => t.Name == name);
    }

    public int AddTag(Tag tag)
    {
        var id = _inner.AddTag(tag);
        lock (_lock)
        {
            tag.Id = id;
            _tags[id] = tag;
        }
        return id;
    }

    public bool DeleteTag(int id)
    {
        if (!_inner.DeleteTag(id)) return false;
        lock (_lock)
        {
            if (_tags.TryGetValue(id, out var tag))
            {
                RemoveTagFromIndex(tag.Name);
                _tags.Remove(id);
            }
        }
        return true;
    }

    private void RemoveTagFromIndex(string tagName)
    {
        _linksByTag.Remove(tagName);
        foreach (var link in _linksByUrl.Values)
        {
            if (link.Tags.Contains(tagName))
                link.Tags.Remove(tagName);
        }
    }

    public bool UpdateTagName(int id, string name)
    {
        if (!_inner.UpdateTagName(id, name)) return false;
        lock (_lock)
        {
            if (!_tags.TryGetValue(id, out var tag)) return false;
            var oldName = tag.Name;
            tag.Name = name;
            if (_linksByTag.TryGetValue(oldName, out var list))
            {
                _linksByTag.Remove(oldName);
                foreach (var link in list)
                {
                    if (link.Tags.Contains(oldName))
                    {
                        link.Tags.Remove(oldName);
                        link.Tags.Add(name);
                    }
                }
                _linksByTag[name] = list;
            }
            foreach (var link in _linksByUrl.Values)
            {
                var idx = link.Tags.IndexOf(oldName);
                if (idx >= 0) link.Tags[idx] = name;
            }
        }
        return true;
    }

    public bool UpdateTagTitle(int id, string title)
    {
        if (!_inner.UpdateTagTitle(id, title)) return false;
        lock (_lock)
        {
            if (_tags.TryGetValue(id, out var tag))
            {
                tag.Title = title;
            }
        }
        return true;
    }

    public List<Link> GetLinksByTag(int tagId)
    {
        EnsureLoaded();
        lock (_lock)
        {
            if (!_tags.TryGetValue(tagId, out var tag)) return new();
            if (_linksByTag.TryGetValue(tag.Name, out var list))
                return list.OrderByDescending(l => l.CreatedAt).ToList();
            return new List<Link>();
        }
    }

    #endregion

    #region 域名 CNAME 映射操作

    public bool AddDomainCname(string domain, string cname)
    {
        if (!_inner.AddDomainCname(domain, cname)) return false;
        lock (_lock) _domainCnames[domain] = cname;
        return true;
    }

    public bool RemoveDomainCname(string domain)
    {
        if (!_inner.RemoveDomainCname(domain)) return false;
        lock (_lock) _domainCnames.Remove(domain);
        return true;
    }

    public string? GetCnameByDomain(string domain)
    {
        EnsureLoaded();
        lock (_lock) return _domainCnames.TryGetValue(domain, out var v) ? v : null;
    }

    public List<string> GetDomainsByCname(string cname)
    {
        EnsureLoaded();
        lock (_lock) return _domainCnames.Where(kv => kv.Value == cname).Select(kv => kv.Key).ToList();
    }

    public List<(string Domain, string Cname)> GetAllDomainCnames()
    {
        EnsureLoaded();
        lock (_lock) return _domainCnames.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public bool RemoveAllCnames()
    {
        if (!_inner.RemoveAllCnames()) return false;
        lock (_lock) _domainCnames.Clear();
        return true;
    }

    #endregion

    public void Reload()
    {
        lock (_lock)
        {
            _sites.Clear();
            _categories.Clear();
            _tags.Clear();
            _domainCnames.Clear();
            _linksByUrl.Clear();
            _linksByCategory.Clear();
            _linksBySite.Clear();
            _linksByTag.Clear();
            _loaded = false;
        }
        EnsureLoaded();
    }
}
