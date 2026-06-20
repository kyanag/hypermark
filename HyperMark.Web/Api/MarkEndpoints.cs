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
                Tags = body.Tags ?? new List<string>(),
                Values = body.Values
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

            if (body.Force)
            {
                // 先按 URL 查找，找到则用其 HyperId 删除
                var link = repo.GetLinkByUrl(body.Url);
                if (link?.Page != null && !string.IsNullOrEmpty(link.Page.HyperId))
                {
                    if (!repo.DeleteLinkByHyperId(link.Page.HyperId))
                        return Results.NotFound(new ErrorResponse("链接不存在"));
                    return Results.Ok(new MessageResponse("取消收藏成功"));
                }

                // URL 未命中，解析 URL 获取 HyperId 再查找删除
                var parser = new UrlParser(repo);
                var page = parser.Parse(body.Url);
                if (!string.IsNullOrEmpty(page.HyperId) && repo.DeleteLinkByHyperId(page.HyperId))
                    return Results.Ok(new MessageResponse("取消收藏成功"));

                return Results.NotFound(new ErrorResponse("链接不存在"));
            }

            if (!repo.DeleteLink(body.Url))
                return Results.NotFound(new ErrorResponse("链接不存在"));

            return Results.Ok(new MessageResponse("取消收藏成功"));
        });

        app.MapGet("/api/is_marked", (string url, Repository repo) =>
        {
            if (string.IsNullOrWhiteSpace(url))
                return Results.BadRequest(new ErrorResponse("缺少 url 参数"));

            // 先直接按 url 查找
            var link = repo.GetLinkByUrl(url);
            if (link is not null)
            {
                var linkTags = repo.GetLinkTags(link.Url);
                return Results.Ok(new MarkStatusResponse(MarkStatus.Full,
                    new MarkLinkInfo(link.Url, link.Title, link.Category, linkTags.Select(t => t.Name).ToList())));
            }

            // 未找到，解析 url 获取 hyperid 再查
            var parser = new UrlParser(repo);
            var page = parser.Parse(url);
            if (!string.IsNullOrEmpty(page.HyperId))
                link = repo.GetLinkByHyperId(page.HyperId);

            if (link is null)
                return Results.Ok(new MarkStatusResponse(MarkStatus.No));

            var tags = repo.GetLinkTags(link.Url);
            return Results.Ok(new MarkStatusResponse(MarkStatus.Half,
                new MarkLinkInfo(link.Url, link.Title, link.Category, tags.Select(t => t.Name).ToList())));
        });
    }
}
