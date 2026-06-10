using HyperMark.Models;

namespace HyperMark.Storage;


public enum MarkStatus
{
    No = 0,     // 收录状态：没有收录

    Half = 1,   // 收录状态：收录了，收录的原始 URL 和当前 URL 不一样，但是标准 URL 一样

    Full = 3,   // 收录状态：收录了，且收录的原始 URL 和当前 URL 一模一样
}

public interface IStorage
{
    /// <summary>
    /// 检查 Site 是否已存在
    /// </summary>
    public bool ExistsSite(Site site);

    /// <summary>
    /// 添加新站点
    /// </summary>
    public bool AddSite(Site site);

    /// <summary>
    /// 获取 URI 的收录状态
    /// </summary>
    public MarkStatus IsMarked(string uri);

    /// <summary>
    /// 获取所有站点列表
    /// </summary>
    public List<Site> Sites();

    /// <summary>
    /// 根据站点名称获取站点
    /// </summary>
    public Site? GetSite(string siteName);

    /// <summary>
    /// 根据站点名称获取该站点下的所有 Link（含 Page 信息）
    /// </summary>
    public List<Link> GetLinksBySite(string siteName);

    /// <summary>
    /// 删除站点及其所有 Link
    /// </summary>
    public bool DeleteSite(string siteName);

    #region 链接收藏操作

    /// <summary>
    /// 根据链接地址查找链接
    /// </summary>
    public Link? GetLinkByUrl(string url);

    /// <summary>
    /// 添加链接（可包含解析后的 Page 信息）
    /// </summary>
    public bool AddLink(Link link);

    /// <summary>
    /// 更新链接的分类
    /// </summary>
    public bool UpdateLinkCategory(string url, string category);

    /// <summary>
    /// 更新链接的标题
    /// </summary>
    public bool UpdateLinkTitle(string url, string title);

    /// <summary>
    /// 更新链接的 Page 信息
    /// </summary>
    public bool UpdateLinkPage(string url, Page page);

    /// <summary>
    /// 更新链接的标签列表
    /// </summary>
    public bool UpdateLinkTags(string url, List<string> tags);

    /// <summary>
    /// 获取所有链接（可按分类筛选）
    /// </summary>
    public List<Link> GetLinks(string? category = null);

    /// <summary>
    /// 删除链接
    /// </summary>
    public bool DeleteLink(string url);

    #endregion

    #region 分类操作

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public List<Category> GetCategories();

    /// <summary>
    /// 根据名称获取分类
    /// </summary>
    public Category? GetCategory(string name);

    /// <summary>
    /// 添加分类
    /// </summary>
    /// <returns>分类名称</returns>
    public string AddCategory(Category category);

    /// <summary>
    /// 移动分类（修改父分类）
    /// </summary>
    public bool MoveCategory(string name, string? newParentName);

    /// <summary>
    /// 删除分类
    /// </summary>
    public bool DeleteCategory(string name);

    /// <summary>
    /// 更新分类名称
    /// </summary>
    public bool UpdateCategoryName(string oldName, string newName);

    #endregion

    #region 标签操作

    /// <summary>
    /// 获取所有标签
    /// </summary>
    public List<Tag> GetTags();

    /// <summary>
    /// 根据 ID 获取标签
    /// </summary>
    public Tag? GetTag(int id);

    /// <summary>
    /// 根据名称查找标签
    /// </summary>
    public Tag? GetTagByName(string name);

    /// <summary>
    /// 添加标签
    /// </summary>
    public int AddTag(Tag tag);

    /// <summary>
    /// 删除标签
    /// </summary>
    public bool DeleteTag(int id);

    /// <summary>
    /// 更新标签名称
    /// </summary>
    public bool UpdateTagName(int id, string name);

    /// <summary>
    /// 更新标签标题（显示用）
    /// </summary>
    public bool UpdateTagTitle(int id, string title);

    /// <summary>
    /// 获取指定标签关联的所有链接
    /// </summary>
    public List<Link> GetLinksByTag(int tagId);

    #endregion

    #region 域名 CNAME 映射操作

    /// <summary>
    /// 添加域名 CNAME 映射（别名域名 → 真实域名）
    /// </summary>
    public bool AddDomainCname(string domain, string cname);

    /// <summary>
    /// 删除域名 CNAME 映射
    /// </summary>
    public bool RemoveDomainCname(string domain);

    /// <summary>
    /// 根据域名获取对应的 CNAME（真实域名）
    /// </summary>
    public string? GetCnameByDomain(string domain);

    /// <summary>
    /// 根据 CNAME（真实域名）获取所有别名域名
    /// </summary>
    public List<string> GetDomainsByCname(string cname);

    /// <summary>
    /// 获取所有域名 CNAME 映射
    /// </summary>
    public List<(string Domain, string Cname)> GetAllDomainCnames();

    /// <summary>
    /// 清空所有域名 CNAME 映射
    /// </summary>
    public bool RemoveAllCnames();

    #endregion

}
