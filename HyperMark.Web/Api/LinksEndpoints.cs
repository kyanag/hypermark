using HyperMark.Parsers;
using HyperMark.Storage;
using HyperMark.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 链接管理端点（/api/links）
/// </summary>
public static class LinksEndpoints
{
    public static void MapLinksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/links");

        group.MapPost("", async (HttpRequest req, Repository repo, UrlParser parser, FileLogger logger) =>
        {
            var body = await req.ReadFromJsonAsync<CreateLinkRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Url))
                return Results.BadRequest(new ErrorResponse("缺少 url 字段"));

            var existing = repo.GetLinkByUrl(body.Url);
            if (existing != null)
                return Results.Conflict(new ErrorResponse("该链接已收藏"));

            var link = new Link
            {
                Url = body.Url,
                Title = string.IsNullOrEmpty(body.Title) ? body.Url : body.Title,
                CreatedAt = DateTime.Now,
                Category = body.Category ?? string.Empty,
                Tags = body.Tags ?? new List<string>()
            };
            repo.AddLink(link);

            try
            {
                var page = parser.Parse(body.Url);
                if (!string.IsNullOrEmpty(page.HyperId))
                {
                    repo.UpdateLinkPage(body.Url, page);
                    link.Page = page;
                }
            }
            catch (Exception ex) { logger.LogError(ex, $"解析链接失败: {body.Url}"); }

            return Results.Created($"/api/links", link);
        });

        group.MapGet("", (string? site, string? category, string? tag, int? limit, int? offset, Repository repo) =>
        {
            List<Link> links;
            if (!string.IsNullOrEmpty(site)) links = repo.GetLinksBySite(site);
            else if (!string.IsNullOrEmpty(category)) links = repo.GetLinks(category);
            else links = repo.GetLinks();

            if (!string.IsNullOrEmpty(tag))
                links = links.Where(l => l.Tags.Contains(tag)).ToList();

            var total = links.Count;
            var safeOffset = Math.Max(0, offset ?? 0);
            var safeLimit = Math.Min(Math.Max(1, limit ?? 50), 200);
            var paged = links.Skip(safeOffset).Take(safeLimit).ToList();

            return Results.Ok(new PagedLinkResponse(paged, total, safeLimit, safeOffset));
        });

        group.MapGet("/{urlEncoded}", (string urlEncoded, Repository repo) =>
        {
            var url = Uri.UnescapeDataString(urlEncoded);
            var link = repo.GetLinkByUrl(url);
            return link is null ? Results.NotFound(new ErrorResponse("链接不存在")) : Results.Ok(link);
        });

        group.MapPut("/{urlEncoded}", async (string urlEncoded, HttpRequest req, Repository repo) =>
        {
            var url = Uri.UnescapeDataString(urlEncoded);
            var link = repo.GetLinkByUrl(url);
            if (link is null) return Results.NotFound(new ErrorResponse("链接不存在"));

            var body = await req.ReadFromJsonAsync<UpdateLinkRequest>();
            if (body is null) return Results.BadRequest(new ErrorResponse("请求体格式错误"));

            if (!string.IsNullOrEmpty(body.Title))
            {
                link.Title = body.Title;
                repo.UpdateLinkTitle(url, body.Title);
            }
            if (body.Category is not null)
            {
                repo.UpdateLinkCategory(url, body.Category);
                link.Category = body.Category;
            }
            if (body.Tags != null)
            {
                var existingTags = repo.GetLinkTags(url);
                foreach (var tag in existingTags) repo.RemoveLinkTag(url, tag.Id);
                foreach (var tagName in body.Tags)
                {
                    var tag = repo.GetTagByName(tagName);
                    if (tag is null)
                    {
                        var newId = repo.AddTag(new Tag { Name = tagName, CreatedAt = DateTime.Now });
                        tag = repo.GetTag(newId);
                    }
                    if (tag != null) repo.AddLinkTag(url, tag.Id);
                }
            }
            return Results.Ok(new LinkUpdatedResponse("更新成功", link));
        });

        group.MapDelete("/{urlEncoded}", (string urlEncoded, Repository repo) =>
        {
            var url = Uri.UnescapeDataString(urlEncoded);
            if (repo.GetLinkByUrl(url) is null)
                return Results.NotFound(new ErrorResponse("链接不存在"));
            repo.DeleteLink(url);
            return Results.Ok(new MessageResponse("删除成功"));
        });

        // 链接-标签子资源
        group.MapGet("/{urlEncoded}/tags", (string urlEncoded, Repository repo) =>
        {
            var url = Uri.UnescapeDataString(urlEncoded);
            if (repo.GetLinkByUrl(url) is null)
                return Results.NotFound(new ErrorResponse("链接不存在"));
            return Results.Ok(repo.GetLinkTags(url));
        });

        group.MapPost("/{urlEncoded}/tags", async (string urlEncoded, HttpRequest req, Repository repo) =>
        {
            var url = Uri.UnescapeDataString(urlEncoded);
            if (repo.GetLinkByUrl(url) is null)
                return Results.NotFound(new ErrorResponse("链接不存在"));

            var body = await req.ReadFromJsonAsync<AddLinkTagsRequest>();
            if (body is null) return Results.BadRequest(new ErrorResponse("请求体格式错误"));

            var added = 0;
            if (body.TagIds is { Count: > 0 })
            {
                foreach (var tagId in body.TagIds)
                {
                    if (repo.GetTag(tagId) is not null && repo.AddLinkTag(url, tagId)) added++;
                }
            }
            if (body.Tags is { Count: > 0 })
            {
                foreach (var tagName in body.Tags)
                {
                    var tag = repo.GetTagByName(tagName);
                    if (tag is null)
                    {
                        var newId = repo.AddTag(new Tag { Name = tagName, CreatedAt = DateTime.Now });
                        tag = repo.GetTag(newId);
                    }
                    if (tag != null && repo.AddLinkTag(url, tag.Id)) added++;
                }
            }
            return Results.Ok(new AddTagsResponse("添加成功", added));
        });

        group.MapDelete("/{urlEncoded}/tags/{tagId}", (string urlEncoded, int tagId, Repository repo) =>
        {
            var url = Uri.UnescapeDataString(urlEncoded);
            if (repo.GetLinkByUrl(url) is null)
                return Results.NotFound(new ErrorResponse("链接不存在"));
            if (repo.GetTag(tagId) is null)
                return Results.NotFound(new ErrorResponse("标签不存在"));
            if (!repo.RemoveLinkTag(url, tagId))
                return Results.NotFound(new ErrorResponse("标签关联不存在"));
            return Results.Ok(new MessageResponse("移除成功"));
        });
    }
}
