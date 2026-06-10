using System.Text.Json;
using System.Text.Json.Serialization;
using HyperMark.Config;
using HyperMark.Models;

namespace HyperMark;

/// <summary>
/// AOT 兼容的 JSON 序列化上下文
/// 所有 JsonSerializer 调用必须使用此上下文，不得使用反射
/// </summary>
[JsonSerializable(typeof(Site))]
[JsonSerializable(typeof(Route))]
[JsonSerializable(typeof(RouteArg))]
[JsonSerializable(typeof(Link))]
[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(Category))]
[JsonSerializable(typeof(Tag))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(List<Category>))]
[JsonSerializable(typeof(List<Tag>))]
[JsonSerializable(typeof(List<Site>))]
[JsonSerializable(typeof(List<Link>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(JsonBookmarkEntry))]
[JsonSerializable(typeof(JsonLinkState))]
[JsonSerializable(typeof(JsonBookmarkLinkState))]
// API 请求类型
[JsonSerializable(typeof(MarkShortcutRequest))]
[JsonSerializable(typeof(UnmarkShortcutRequest))]
[JsonSerializable(typeof(SiteUpdateRequest))]
[JsonSerializable(typeof(AddDomainRequest))]
[JsonSerializable(typeof(CreateLinkRequest))]
[JsonSerializable(typeof(UpdateLinkRequest))]
[JsonSerializable(typeof(AddLinkTagsRequest))]
[JsonSerializable(typeof(BatchParseRequestBody))]
[JsonSerializable(typeof(CreateCategoryReq))]
[JsonSerializable(typeof(RenameCategoryReq))]
[JsonSerializable(typeof(MoveCategoryReq))]
[JsonSerializable(typeof(CreateTagReq))]
[JsonSerializable(typeof(UpdateTagReq))]
[JsonSerializable(typeof(AddCnameRequest))]
// API 响应类型
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(SiteBrief))]
[JsonSerializable(typeof(TagBrief))]
[JsonSerializable(typeof(MarkStatusResponse))]
[JsonSerializable(typeof(MarkLinkInfo))]
[JsonSerializable(typeof(MarkCreatedResponse))]
[JsonSerializable(typeof(LinkUpdatedResponse))]
[JsonSerializable(typeof(PagedLinkResponse))]
[JsonSerializable(typeof(ParsePageInfo))]
[JsonSerializable(typeof(ParseLinkInfo))]
[JsonSerializable(typeof(ParseResultResponse))]
[JsonSerializable(typeof(ParseMatchResponse))]
[JsonSerializable(typeof(ParseBatchResponse))]
[JsonSerializable(typeof(CategoryCreatedResponse))]
[JsonSerializable(typeof(TagCreatedResponse))]
[JsonSerializable(typeof(SiteDomainsResponse))]
[JsonSerializable(typeof(AddTagsResponse))]
[JsonSerializable(typeof(DomainCnameResponse))]
[JsonSerializable(typeof(IEnumerable<SiteBrief>))]
[JsonSerializable(typeof(IEnumerable<TagBrief>))]
[JsonSerializable(typeof(IEnumerable<Link>))]
[JsonSerializable(typeof(IEnumerable<Category>))]
[JsonSerializable(typeof(IEnumerable<Tag>))]
[JsonSerializable(typeof(IEnumerable<Site>))]
public partial class HyperMarkJsonContext : JsonSerializerContext
{
    public static readonly HyperMarkJsonContext Instance = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
}

/// <summary>
/// 书签文件序列化模型（对应 LocalStorage.BookmarkEntry）
/// </summary>
public class JsonBookmarkEntry
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Page? Page { get; set; }
}

/// <summary>
/// 链接状态序列化模型（对应 ActionLogger.LinkState）
/// </summary>
public class JsonLinkState
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool Deleted { get; set; }
}

/// <summary>
/// 书签链接状态（用于 replay）
/// </summary>
public class JsonBookmarkLinkState
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Category { get; set; } = string.Empty;
}
