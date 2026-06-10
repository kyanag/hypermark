using System.Text;
using System.Text.Json;
using HyperMark.Models;

namespace HyperMark.Storage;

/// <summary>
/// 基于文件系统的存储实现（无缓存，纯文件 I/O）
/// 数据存储在 ~/.hypermark/ 目录下的文件中
/// </summary>
public class LocalStorage : IStorage
{
    private readonly string _basePath;
    private readonly string _sitesDir;
    private readonly string _bookmarksDir;
    private readonly string _categoriesFile;
    private readonly string _tagsFile;
    private readonly string _domainCnamesFile;

    private static readonly JsonSerializerOptions JsonOptions = HyperMarkJsonContext.Instance.Options;

    public LocalStorage(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hypermark");
        _sitesDir = Path.Combine(_basePath, "sites");
        _bookmarksDir = Path.Combine(_basePath, "bookmarks");
        _categoriesFile = Path.Combine(_basePath, "categories.json");
        _tagsFile = Path.Combine(_basePath, "tags.json");
        _domainCnamesFile = Path.Combine(_basePath, "domain_cnames.json");

        InitializeDirectories();
        EnsureBuiltInTags();
    }

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(_sitesDir);
        Directory.CreateDirectory(_bookmarksDir);
    }

    /// <summary>
    /// 确保内置标签存在
    /// </summary>
    private void EnsureBuiltInTags()
    {
        var tags = GetTags();
        foreach (var builtinName in BuiltInTags.Names)
        {
            if (!tags.Any(t => t.Name == builtinName))
            {
                tags.Add(new Tag
                {
                    Name = builtinName,
                    Title = builtinName switch
                    {
                        BuiltInTags.PageParsed.Name => BuiltInTags.PageParsed.Title,
                        BuiltInTags.NoSite.Name => BuiltInTags.NoSite.Title,
                        _ => builtinName
                    },
                    CreatedAt = DateTime.Now
                });
            }
        }
        SaveTags(tags);
    }

    #region 站点操作

    public bool AddSite(Site site)
    {
        var filePath = Path.Combine(_sitesDir, $"{site.Name}.json");
        if (File.Exists(filePath)) return false;
        var json = JsonSerializer.Serialize(site, HyperMarkJsonContext.Instance.Site);
        AtomicWrite(filePath, json);
        return true;
    }

    public bool ExistsSite(Site site)
    {
        return File.Exists(Path.Combine(_sitesDir, $"{site.Name}.json"));
    }

    public List<Site> Sites()
    {
        var sites = new List<Site>();
        foreach (var file in Directory.GetFiles(_sitesDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var site = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.Site);
                if (site != null && !string.IsNullOrEmpty(site.Name))
                    sites.Add(site);
            }
            catch { }
        }
        return sites;
    }

    public Site? GetSite(string siteName)
    {
        var filePath = Path.Combine(_sitesDir, $"{siteName}.json");
        if (!File.Exists(filePath)) return null;
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.Site);
    }

    public bool DeleteSite(string siteName)
    {
        var siteFile = Path.Combine(_sitesDir, $"{siteName}.json");
        if (!File.Exists(siteFile)) return false;

        var suffix = $".deleted_{DateTime.Now:yyyyMMdd_HHmmss}";
        File.Move(siteFile, siteFile + suffix, true);

        // 删除该站点下所有 Link 文件
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Page != null && entry.Page.Site == siteName)
                {
                    File.Delete(file);
                }
            }
            catch { }
        }
        return true;
    }

    #endregion

    #region 收录状态

    public MarkStatus IsMarked(string uri)
    {
        var parser = new Parsers.UrlParser(new Repository(this));
        var page = parser.Parse(uri);
        return CheckMarkStatus(page);
    }

    private MarkStatus CheckMarkStatus(Page page)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Page == null) continue;
                if (entry.Page.Site == page.Site && entry.Page.HyperId == page.HyperId)
                    return MarkStatus.Full;
            }
            catch { }
        }
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Page != null && entry.Page.Site == page.Site && entry.Page.Std == page.Std)
                    return MarkStatus.Half;
            }
            catch { }
        }
        return MarkStatus.No;
    }

    #endregion

    #region 链接操作

    public Link? GetLinkByUrl(string url)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Url == url)
                    return ToLink(entry);
            }
            catch { }
        }
        return null;
    }

    public bool AddLink(Link link)
    {
        // 检查是否已存在
        if (GetLinkByUrl(link.Url) != null) return false;

        // 同步内置标签
        SyncBuiltInTags(link);

        var key = MakeFileName(link);
        var categoryDir = GetCategoryDirPath(link.Category);
        Directory.CreateDirectory(categoryDir);

        var filePath = Path.Combine(categoryDir, $"{key}.json");
        var entry = new JsonBookmarkEntry
        {
            Url = link.Url,
            Name = link.Name,
            Title = link.Title,
            CreatedAt = link.CreatedAt,
            Category = link.Category,
            Tags = link.Tags,
            Page = link.Page
        };

        AtomicWrite(filePath, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
        return true;
    }

    public bool UpdateLinkCategory(string url, string category)
    {
        var (file, entry) = FindLinkByUrl(url);
        if (entry == null || file == null) return false;

        entry.Category = category;
        var newDir = GetCategoryDirPath(category);
        Directory.CreateDirectory(newDir);
        var newFilePath = Path.Combine(newDir, $"{Path.GetFileNameWithoutExtension(file)}.json");
        AtomicWrite(newFilePath, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
        if (file != newFilePath) File.Delete(file);
        return true;
    }

    public bool UpdateLinkPage(string url, Page page)
    {
        var (file, entry) = FindLinkByUrl(url);
        if (entry == null || file == null) return false;

        entry.Page = page;
        var link = ToLink(entry);
        SyncBuiltInTags(link);
        entry.Tags = link.Tags;
        AtomicWrite(file, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
        return true;
    }

    public bool UpdateLinkTitle(string url, string title)
    {
        var (file, entry) = FindLinkByUrl(url);
        if (entry == null || file == null) return false;

        entry.Title = title;
        AtomicWrite(file, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
        return true;
    }

    public bool UpdateLinkTags(string url, List<string> tags)
    {
        var (file, entry) = FindLinkByUrl(url);
        if (entry == null || file == null) return false;

        entry.Tags = tags;
        AtomicWrite(file, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
        return true;
    }

    public List<Link> GetLinks(string? category = null)
    {
        var links = new List<Link>();
        var searchDir = _bookmarksDir;

        if (!string.IsNullOrEmpty(category))
        {
            var cats = GetCategories();
            searchDir = BuildCategoryDirPath(category, cats);
            if (!Directory.Exists(searchDir)) return links;
        }

        foreach (var file in Directory.GetFiles(searchDir, "*.json", SearchOption.AllDirectories))
        {
            if (!string.IsNullOrEmpty(category))
            {
                var relativePath = Path.GetRelativePath(searchDir, file);
                if (!relativePath.StartsWith(category + Path.DirectorySeparatorChar) &&
                    !relativePath.Equals(Path.GetFileName(file)))
                    continue;
            }

            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry != null && (string.IsNullOrEmpty(category) || entry.Category == category))
                    links.Add(ToLink(entry));
            }
            catch { }
        }

        links.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return links;
    }

    public List<Link> GetLinksBySite(string siteName)
    {
        var links = new List<Link>();
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Page != null && entry.Page.Site == siteName)
                    links.Add(ToLink(entry));
            }
            catch { }
        }
        links.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return links;
    }

    public bool DeleteLink(string url)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Url == url)
                {
                    File.Delete(file);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    #endregion

    #region 分类操作

    public List<Category> GetCategories()
    {
        if (!File.Exists(_categoriesFile)) return new();
        var json = File.ReadAllText(_categoriesFile, Encoding.UTF8);
        return JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.ListCategory) ?? new();
    }

    public Category? GetCategory(string name)
    {
        return GetCategories().FirstOrDefault(c => c.Name == name);
    }

    public string AddCategory(Category category)
    {
        var categories = GetCategories();
        if (categories.Any(c => c.Name == category.Name)) return category.Name;

        category.CreatedAt = DateTime.Now;
        categories.Add(category);
        SaveCategories(categories);

        var dirPath = BuildCategoryDirPath(category.Name, categories);
        Directory.CreateDirectory(dirPath);
        return category.Name;
    }

    public bool MoveCategory(string name, string? newParentName)
    {
        var categories = GetCategories();
        var category = categories.FirstOrDefault(c => c.Name == name);
        if (category == null) return false;

        category.ParentId = newParentName;
        SaveCategories(categories);

        var oldPath = BuildCategoryDirPathByName(name, categories);
        var newPath = BuildCategoryDirPath(name, categories);
        MoveDirectory(oldPath, newPath);
        return true;
    }

    public bool DeleteCategory(string name)
    {
        var categories = GetCategories();
        if (categories.RemoveAll(c => c.Name == name) == 0) return false;

        foreach (var cat in categories.Where(c => c.ParentId == name))
            cat.ParentId = null;

        SaveCategories(categories);

        var dirPath = BuildCategoryDirPathByName(name, categories);
        if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);
        return true;
    }

    public bool UpdateCategoryName(string oldName, string newName)
    {
        var categories = GetCategories();
        var category = categories.FirstOrDefault(c => c.Name == oldName);
        if (category == null) return false;
        if (categories.Any(c => c.Name == newName)) return false;

        category.Name = newName;
        foreach (var cat in categories.Where(c => c.ParentId == oldName))
            cat.ParentId = newName;

        SaveCategories(categories);

        var oldDirPath = BuildCategoryDirPathByName(oldName, categories);
        var newDirPath = BuildCategoryDirPath(newName, categories);
        MoveDirectory(oldDirPath, newDirPath);

        // 更新所有 Link JSON 中的 category 字段
        UpdateLinksCategory(oldName, newName);
        return true;
    }

    private void UpdateLinksCategory(string oldCategory, string newCategory)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Category == oldCategory)
                {
                    entry.Category = newCategory;
                    var key = Path.GetFileNameWithoutExtension(file);
                    var newDir = BuildCategoryDirPath(newCategory, GetCategories());
                    Directory.CreateDirectory(newDir);
                    var newFilePath = Path.Combine(newDir, $"{key}.json");
                    AtomicWrite(newFilePath, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
                    if (file != newFilePath) File.Delete(file);
                }
            }
            catch { }
        }
    }

    #endregion

    #region 标签操作

    public List<Tag> GetTags()
    {
        if (!File.Exists(_tagsFile)) return new();
        var json = File.ReadAllText(_tagsFile, Encoding.UTF8);
        return JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.ListTag) ?? new();
    }

    public Tag? GetTag(int id) => GetTags().FirstOrDefault(t => t.Id == id);
    public Tag? GetTagByName(string name) => GetTags().FirstOrDefault(t => t.Name == name);

    public int AddTag(Tag tag)
    {
        var tags = GetTags();
        var maxId = tags.Select(t => t.Id).DefaultIfEmpty(0).Max();
        tag.Id = maxId + 1;
        tag.CreatedAt = DateTime.Now;
        tags.Add(tag);
        SaveTags(tags);
        return tag.Id;
    }

    public bool DeleteTag(int id)
    {
        var tags = GetTags();
        var tag = tags.FirstOrDefault(t => t.Id == id);
        if (tag == null) return false;

        RemoveTagFromAllLinks(tag.Name);
        tags.Remove(tag);
        SaveTags(tags);
        return true;
    }

    public bool UpdateTagName(int id, string name)
    {
        var tags = GetTags();
        var tag = tags.FirstOrDefault(t => t.Id == id);
        if (tag == null) return false;

        var oldName = tag.Name;
        tag.Name = name;
        SaveTags(tags);
        UpdateTagNameInAllLinks(oldName, name);
        return true;
    }

    public bool UpdateTagTitle(int id, string title)
    {
        var tags = GetTags();
        var tag = tags.FirstOrDefault(t => t.Id == id);
        if (tag == null) return false;

        tag.Title = title;
        SaveTags(tags);
        return true;
    }

    public List<Link> GetLinksByTag(int tagId)
    {
        var tag = GetTag(tagId);
        if (tag == null) return new();
        return GetLinks().Where(l => l.Tags.Contains(tag.Name)).ToList();
    }

    private void RemoveTagFromAllLinks(string tagName)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Tags != null && entry.Tags.Contains(tagName))
                {
                    entry.Tags.Remove(tagName);
                    AtomicWrite(file, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
                }
            }
            catch { }
        }
    }

    private void UpdateTagNameInAllLinks(string oldName, string newName)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Tags != null)
                {
                    var idx = entry.Tags.IndexOf(oldName);
                    if (idx >= 0)
                    {
                        entry.Tags[idx] = newName;
                        AtomicWrite(file, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
                    }
                }
            }
            catch { }
        }
    }

    #endregion

    #region 域名 CNAME 映射操作

    public bool AddDomainCname(string domain, string cname)
    {
        var dict = GetDomainCnames();
        dict[domain] = cname;
        AtomicWrite(_domainCnamesFile, JsonSerializer.Serialize(dict, HyperMarkJsonContext.Instance.DictionaryStringString));
        return true;
    }

    public bool RemoveDomainCname(string domain)
    {
        var dict = GetDomainCnames();
        if (!dict.Remove(domain)) return false;
        AtomicWrite(_domainCnamesFile, JsonSerializer.Serialize(dict, HyperMarkJsonContext.Instance.DictionaryStringString));
        return true;
    }

    public string? GetCnameByDomain(string domain)
    {
        var dict = GetDomainCnames();
        return dict.TryGetValue(domain, out var v) ? v : null;
    }

    public List<string> GetDomainsByCname(string cname)
    {
        var dict = GetDomainCnames();
        return dict.Where(kv => kv.Value == cname).Select(kv => kv.Key).ToList();
    }

    public List<(string Domain, string Cname)> GetAllDomainCnames()
    {
        var dict = GetDomainCnames();
        return dict.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public bool RemoveAllCnames()
    {
        AtomicWrite(_domainCnamesFile, "{}");
        return true;
    }

    private Dictionary<string, string> GetDomainCnames()
    {
        if (!File.Exists(_domainCnamesFile)) return new();
        var json = File.ReadAllText(_domainCnamesFile, Encoding.UTF8);
        return JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.DictionaryStringString) ?? new();
    }

    #endregion

    #region 辅助方法

    private static string MakeFileName(Link link)
    {
        var safeTitle = SanitizeFileName(link.Title);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
        return $"{safeTitle}_{timestamp}";
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "untitled";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.TrimEnd('.');
    }

    private static void AtomicWrite(string filePath, string content)
    {
        var tempFile = filePath + ".tmp";
        File.WriteAllText(tempFile, content, Encoding.UTF8);
        File.Move(tempFile, filePath, true);
    }

    private static Link ToLink(JsonBookmarkEntry entry)
    {
        return new Link
        {
            Url = entry.Url,
            Name = entry.Name,
            Title = entry.Title,
            CreatedAt = entry.CreatedAt,
            Category = entry.Category,
            Tags = entry.Tags ?? new(),
            Page = entry.Page
        };
    }

    private (string? file, JsonBookmarkEntry? entry) FindLinkByUrl(string url)
    {
        foreach (var file in Directory.GetFiles(_bookmarksDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var entry = JsonSerializer.Deserialize(json, HyperMarkJsonContext.Instance.JsonBookmarkEntry);
                if (entry?.Url == url) return (file, entry);
            }
            catch { }
        }
        return (null, null);
    }

    private string GetCategoryDirPath(string? category)
    {
        if (string.IsNullOrEmpty(category)) return _bookmarksDir;
        var cats = GetCategories();
        return BuildCategoryDirPath(category, cats);
    }

    internal static string BuildCategoryDirPath(string category, List<Category> cats)
    {
        var names = new List<string>();
        var current = category;
        var visited = new HashSet<string>();
        while (!string.IsNullOrEmpty(current))
        {
            if (visited.Contains(current)) break;
            visited.Add(current);
            names.Add(current);
            var cat = cats.FirstOrDefault(c => c.Name == current);
            current = cat?.ParentId;
        }
        names.Reverse();
        return Path.Combine(new[] { cats.Count > 0 ? GetBookmarksDir(cats) : ".hypermark/bookmarks" }.Concat(names).ToArray());
    }

    internal static string BuildCategoryDirPathByName(string name, List<Category> cats)
    {
        return BuildCategoryDirPath(name, cats);
    }

    private static string GetBookmarksDir(List<Category> _)
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hypermark", "bookmarks");
    }

    private static void MoveDirectory(string oldPath, string newPath)
    {
        if (!Directory.Exists(oldPath) || oldPath == newPath) return;
        if (Directory.Exists(newPath))
        {
            foreach (var file in Directory.GetFiles(oldPath, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(oldPath, file);
                var dest = Path.Combine(newPath, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(file, dest, true);
            }
            Directory.Delete(oldPath, true);
        }
        else
        {
            Directory.Move(oldPath, newPath);
        }
    }

    private void SaveCategories(List<Category> categories)
    {
        var json = JsonSerializer.Serialize(categories.OrderBy(c => c.CreatedAt).ToList(), HyperMarkJsonContext.Instance.ListCategory);
        AtomicWrite(_categoriesFile, json);
    }

    private void SaveTags(List<Tag> tags)
    {
        var json = JsonSerializer.Serialize(tags.OrderBy(t => t.Id).ToList(), HyperMarkJsonContext.Instance.ListTag);
        AtomicWrite(_tagsFile, json);
    }

    /// <summary>
    /// 根据 Link 的 Page 信息自动同步内置标签
    /// </summary>
    private static void SyncBuiltInTags(Link link)
    {
        var wanted = BuiltInTags.ResolveForLink(link);
        // 移除不需要的内置标签
        link.Tags.RemoveAll(t => BuiltInTags.Names.Contains(t) && !wanted.Contains(t));
        // 添加需要的内置标签
        foreach (var tag in wanted)
        {
            if (!link.Tags.Contains(tag))
                link.Tags.Add(tag);
        }
    }

    public void Reload() { }

    #endregion
}
