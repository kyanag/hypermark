using HyperMark.Storage;
using HyperMark.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 标签管理端点（/api/tags）
/// </summary>
public static class TagsEndpoints
{
    public static void MapTagsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tags");

        group.MapGet("", (string? q, Repository repo) =>
        {
            var tags = repo.GetTags();
            if (!string.IsNullOrEmpty(q))
            {
                tags = tags.Where(t =>
                    t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(t.Title) && t.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            return Results.Ok(tags);
        });

        group.MapGet("/autocomplete", (string q, int? limit, Repository repo) =>
        {
            var tags = repo.GetTags()
                .Where(t =>
                    t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(t.Title) && t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .Take(limit ?? 10)
                .Select(t => new TagBrief(t.Id, t.Name, t.Title))
                .ToList();
            return Results.Ok(tags);
        });

        group.MapGet("/{id}", (int id, Repository repo) =>
        {
            var tag = repo.GetTag(id);
            return tag is null ? Results.NotFound(new ErrorResponse("标签不存在")) : Results.Ok(tag);
        });

        group.MapPost("", async (HttpRequest req, Repository repo) =>
        {
            var body = await req.ReadFromJsonAsync<CreateTagReq>();
            if (body is null || string.IsNullOrEmpty(body.Name))
                return Results.BadRequest(new ErrorResponse("缺少 name 字段"));
            var existing = repo.GetTagByName(body.Name);
            if (existing != null) return Results.Conflict(new ErrorResponse("标签已存在"));

            var tag = new Tag { Name = body.Name, Title = body.Title ?? string.Empty, CreatedAt = DateTime.Now };
            var tagId = repo.AddTag(tag);
            return Results.Created($"/api/tags/{tagId}", new TagCreatedResponse(tagId, tag.Name, tag.Title));
        });

        group.MapPut("/{id}", async (int id, HttpRequest req, Repository repo) =>
        {
            var tag = repo.GetTag(id);
            if (tag is null) return Results.NotFound(new ErrorResponse("标签不存在"));
            var body = await req.ReadFromJsonAsync<UpdateTagReq>();
            if (body is null) return Results.BadRequest(new ErrorResponse("请求体格式错误"));
            if (!string.IsNullOrEmpty(body.Title) && body.Title != tag.Title) repo.UpdateTagTitle(id, body.Title);
            if (!string.IsNullOrEmpty(body.Name) && body.Name != tag.Name) repo.UpdateTagName(id, body.Name);
            return Results.Ok(new MessageResponse("更新成功"));
        });

        group.MapGet("/{id}/links", (int id, Repository repo) =>
        {
            var tag = repo.GetTag(id);
            if (tag is null) return Results.NotFound(new ErrorResponse("标签不存在"));
            return Results.Ok(repo.GetLinksByTag(id));
        });
    }
}
