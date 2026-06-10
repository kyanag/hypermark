using HyperMark.Parsers;
using HyperMark.Storage;
using HyperMark.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 收藏快捷端点（/api/mark, /api/unmark, /api/is_marked）
/// </summary>
public static class MarkEndpoints
{
    public static void MapMarkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/mark", async (HttpRequest req, Repository repo, BackgroundLinkProcessor processor, FileLogger logger) =>
        {
            var body = await req.ReadFromJsonAsync<MarkShortcutRequest>();
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
            try { await processor.Enqueue(link); }
            catch (Exception ex) { logger.LogError(ex, $"入队链接失败: {link.Url}"); }

            return Results.Created("/api/mark", new MarkCreatedResponse("收藏成功",
                new MarkLinkInfo(link.Url, link.Title, link.Category, link.Tags)));
        });

        app.MapPost("/api/unmark", async (HttpRequest req, Repository repo) =>
        {
            var body = await req.ReadFromJsonAsync<UnmarkShortcutRequest>();
            if (body is null || string.IsNullOrEmpty(body.Url))
                return Results.BadRequest(new ErrorResponse("缺少 url 字段"));

            if (!repo.DeleteLink(body.Url))
                return Results.NotFound(new ErrorResponse("链接不存在"));

            return Results.Ok(new MessageResponse("取消收藏成功"));
        });

        app.MapGet("/api/is_marked", (string url, Repository repo) =>
        {
            var link = repo.GetLinkByUrl(url);
            if (link is null)
                return Results.Ok(new MarkStatusResponse(false));

            var linkTags = repo.GetLinkTags(url);
            return Results.Ok(new MarkStatusResponse(true,
                new MarkLinkInfo(link.Url, link.Title, link.Category, linkTags.Select(t => t.Name).ToList())));
        });
    }
}
