using HyperMark.Models;
using HyperMark.Storage;

namespace HyperMark;

// 基础响应
public record ErrorResponse(string Error);
public record MessageResponse(string Message);
public record HealthResponse(string Status, string Version);

// 站点
public record SiteBrief(string Name, string Title);

// 标签
public record TagBrief(int Id, string Name, string Title);

// 收藏
public record MarkLinkInfo(string Url, string Title, string Category, List<string> Tags);
public record MarkStatusResponse(MarkStatus Status, MarkLinkInfo? Link = null);
public record MarkCreatedResponse(string Message, MarkLinkInfo Link);

// 链接
public record LinkUpdatedResponse(string Message, Link Link);
public record PagedLinkResponse(List<Link> Data, int Total, int Limit, int Offset);

// 解析
public record ParsePageInfo(string HyperId, string Site, string Std, Dictionary<string, string> Args, List<string> Tags, string Route);
public record ParseLinkInfo(string Url, string Title, string? Category, DateTime CreatedAt, List<string> Tags);
public record ParseResultResponse(ParsePageInfo Page, ParseLinkInfo? Link);
public record ParseMatchResponse(bool Matched, string? Site, string? Route);
public record ParseBatchResponse(string Message, int Total, int Success, int Failed);

// 分类/标签操作
public record CategoryCreatedResponse(string Name, string? ParentId);
public record TagCreatedResponse(int Id, string Name, string Title);

// 其他
public record SiteDomainsResponse(string Name, List<string> Domains);
public record AddTagsResponse(string Message, int Added);
public record DomainCnameResponse(string Domain, string Cname);
