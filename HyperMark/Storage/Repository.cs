using HyperMark.Models;

namespace HyperMark.Storage;

/// <summary>
/// 数据仓储实现
/// 封装 IStorage，提供业务逻辑处理
/// Link 是核心实体，Page 是 Link 的可选属性
/// </summary>
public class Repository
{
    private readonly IStorage _storage;

    public Repository(IStorage storage)
    {
        _storage = storage;
    }

    #region Site 操作

    public List<Site> GetAllSites() => _storage.Sites();
    public Site? GetSite(string siteName) => _storage.GetSite(siteName);
    public bool AddSite(Site site) => _storage.AddSite(site);
    public bool ExistsSite(string siteName) => _storage.GetSite(siteName) != null;
    public bool DeleteSite(string siteName) => _storage.DeleteSite(siteName);

    #endregion

    #region 链接操作

    public Link? GetLinkByUrl(string url) => _storage.GetLinkByUrl(url);
    public bool AddLink(Link link) => _storage.AddLink(link);
    public bool UpdateLinkCategory(string url, string category) => _storage.UpdateLinkCategory(url, category);
    public bool UpdateLinkTitle(string url, string title) => _storage.UpdateLinkTitle(url, title);
    public bool UpdateLinkPage(string url, Page page) => _storage.UpdateLinkPage(url, page);
    public List<Link> GetLinks(string? category = null) => _storage.GetLinks(category);
    public List<Link> GetLinksBySite(string siteName) => _storage.GetLinksBySite(siteName);
    public bool DeleteLink(string url) => _storage.DeleteLink(url);

    #endregion

    #region 收录状态

    public MarkStatus IsLinkMarked(string uri) => _storage.IsMarked(uri);

    #endregion

    #region 分类操作

    public List<Category> GetCategories() => _storage.GetCategories();
    public Category? GetCategory(string name) => _storage.GetCategory(name);
    public string AddCategory(Category category) => _storage.AddCategory(category);
    public bool DeleteCategory(string name, bool force = false)
    {
        // 检查是否存在子分类
        var categories = _storage.GetCategories();
        if (categories.Any(c => c.ParentId == name) && !force)
            throw new InvalidOperationException("该分类下存在子分类，无法删除");

        // 检查是否存在链接
        var links = _storage.GetLinks(name);
        if (links.Count > 0 && !force)
            throw new InvalidOperationException("该分类下存在收藏链接，无法删除");

        return _storage.DeleteCategory(name);
    }
    public bool UpdateCategoryName(string oldName, string newName) => _storage.UpdateCategoryName(oldName, newName);

    public bool MoveCategory(string name, string? newParentName)
    {
        // 不能移动到自己
        if (newParentName == name) return false;
        if (newParentName == null) return _storage.MoveCategory(name, null);

        // 检查新父分类是否存在
        if (_storage.GetCategory(newParentName) == null) return false;

        // 防止循环：检查移动的节点是否是新父节点的祖先
        if (IsAncestor(name, newParentName)) return false;

        return _storage.MoveCategory(name, newParentName);
    }

    /// <summary>
    /// 判断 ancestorName 是否为 descendantName 的祖先
    /// </summary>
    private bool IsAncestor(string ancestorName, string descendantName)
    {
        var visited = new HashSet<string>();
        var current = _storage.GetCategory(descendantName);
        while (current != null && current.ParentId != null)
        {
            if (visited.Contains(current.Name)) return true; // 已有循环
            visited.Add(current.Name);
            if (current.ParentId == ancestorName) return true;
            current = _storage.GetCategory(current.ParentId);
        }
        return false;
    }

    #endregion

    #region 标签操作

    public List<Tag> GetTags() => _storage.GetTags();
    public Tag? GetTag(int id) => _storage.GetTag(id);
    public Tag? GetTagByName(string name) => _storage.GetTagByName(name);
    public int AddTag(Tag tag) => _storage.AddTag(tag);
    public bool DeleteTag(int id) => _storage.DeleteTag(id);
    public bool UpdateTagName(int id, string name) => _storage.UpdateTagName(id, name);
    public bool UpdateTagTitle(int id, string title) => _storage.UpdateTagTitle(id, title);

    /// <summary>
    /// 获取链接的标签（从 Link.Tags 中映射为 Tag 对象）
    /// </summary>
    public List<Tag> GetLinkTags(string url)
    {
        var link = _storage.GetLinkByUrl(url);
        if (link == null) return new List<Tag>();
        return link.Tags.Select(name => _storage.GetTagByName(name))
            .Where(t => t != null).ToList()!;
    }

    /// <summary>
    /// 为链接添加标签（按名称）
    /// </summary>
    public bool AddLinkTag(string url, int tagId)
    {
        var tag = _storage.GetTag(tagId);
        if (tag == null) return false;

        var link = _storage.GetLinkByUrl(url);
        if (link == null) return false;

        if (link.Tags.Contains(tag.Name)) return true; // 已存在

        link.Tags.Add(tag.Name);
        // 直接更新 Link 的 Tags — 通过重新添加 Link
        return _storage.UpdateLinkTags(url, link.Tags);
    }

    /// <summary>
    /// 移除链接的标签（按名称）
    /// </summary>
    public bool RemoveLinkTag(string url, int tagId)
    {
        var tag = _storage.GetTag(tagId);
        if (tag == null) return false;

        var link = _storage.GetLinkByUrl(url);
        if (link == null) return false;

        if (!link.Tags.Remove(tag.Name)) return false;

        return _storage.UpdateLinkTags(url, link.Tags);
    }

    public List<Link> GetLinksByTag(int tagId) => _storage.GetLinksByTag(tagId);

    #endregion

    #region 域名映射操作

    public bool AddDomainCname(string domain, string cname) => _storage.AddDomainCname(domain, cname);
    public bool RemoveDomainCname(string domain) => _storage.RemoveDomainCname(domain);
    public string? GetCnameByDomain(string domain) => _storage.GetCnameByDomain(domain);
    public List<string> GetDomainsByCname(string cname) => _storage.GetDomainsByCname(cname);
    public List<(string Domain, string Cname)> GetAllDomainCnames() => _storage.GetAllDomainCnames();
    public bool RemoveAllCnames() => _storage.RemoveAllCnames();

    #endregion
}
