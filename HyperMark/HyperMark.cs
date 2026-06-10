using HyperMark.Models;
using HyperMark.Parsers;
using HyperMark.Storage;

namespace HyperMark;

public class HyperMarkApp
{
    private readonly Repository _repository;
    private readonly UrlParser _parser;

    public HyperMarkApp(Repository? repository = null)
    {
        _repository = repository ?? new Repository(new ActionLogger(new CacheStorage(new LocalStorage())));
        _parser = new UrlParser(_repository);
    }

    public void AddSite(Site site)
    {
        _repository.AddSite(site);
    }

    /// <summary>
    /// 添加链接收藏（创建 Link + 解析后的 Page）
    /// </summary>
    /// <param name="uri">要收藏的 URL</param>
    /// <returns>添加结果：success/new/duplicate</returns>
    public AddUriResult AddUri(string uri)
    {
        // 解析 URL 获取 Page 信息
        var page = _parser.Parse(uri);

        // 检查是否已收藏（通过 URL）
        var existing = _repository.GetLinkByUrl(uri);
        if (existing != null)
        {
            return AddUriResult.Duplicate;
        }

        // 创建 Link（包含 Page 信息）
        var link = new Link
        {
            Url = uri,
            Title = page.Std,
            CreatedAt = DateTime.Now,
            Category = "",
            Page = page
        };
        _repository.AddLink(link);
        return AddUriResult.Success;
    }

    /// <summary>
    /// 检查链接是否已收藏
    /// </summary>
    /// <param name="uri">要检查的 URL</param>
    /// <returns>收录状态</returns>
    public MarkStatus IsMarked(string uri)
    {
        return _repository.IsLinkMarked(uri);
    }
}

/// <summary>
/// 添加链接结果
/// </summary>
public enum AddUriResult
{
    /// <summary>
    /// 添加成功
    /// </summary>
    Success,

    /// <summary>
    /// 已存在（重复）
    /// </summary>
    Duplicate
}
