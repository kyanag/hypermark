using HyperMark.Storage;
using HyperMark.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 分类管理端点（/api/categories）
/// </summary>
public static class CategoriesEndpoints
{
    public static void MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories");

        group.MapGet("", (Repository repo) => repo.GetCategories());

        group.MapGet("/{name}", (string name, Repository repo) =>
        {
            var category = repo.GetCategory(name);
            return category is null ? Results.NotFound(new ErrorResponse("分类不存在")) : Results.Ok(category);
        });

        group.MapPost("", async (HttpRequest req, Repository repo) =>
        {
            var body = await req.ReadFromJsonAsync<CreateCategoryReq>();
            if (body is null || string.IsNullOrEmpty(body.Name))
                return Results.BadRequest(new ErrorResponse("缺少 name 字段"));

            var existing = repo.GetCategory(body.Name);
            if (existing != null) return Results.Conflict(new ErrorResponse("分类已存在"));

            var category = new Category { Name = body.Name, ParentId = body.ParentName, CreatedAt = DateTime.Now };
            var resultName = repo.AddCategory(category);
            return Results.Created($"/api/categories/{Uri.EscapeDataString(resultName)}", new CategoryCreatedResponse(category.Name, category.ParentId));
        });

        group.MapPut("/{name}", async (string name, HttpRequest req, Repository repo) =>
        {
            var body = await req.ReadFromJsonAsync<RenameCategoryReq>();
            if (body is null || string.IsNullOrEmpty(body.NewName))
                return Results.BadRequest(new ErrorResponse("缺少 newName 字段"));
            if (!repo.UpdateCategoryName(name, body.NewName))
                return Results.NotFound(new ErrorResponse("分类不存在"));
            return Results.Ok(new MessageResponse("更新成功"));
        });

        group.MapPatch("/{name}/move", async (string name, HttpRequest req, Repository repo) =>
        {
            var body = await req.ReadFromJsonAsync<MoveCategoryReq>();
            if (body is null) return Results.BadRequest(new ErrorResponse("缺少 newParentName 字段"));
            if (body.NewParentName == name) return Results.BadRequest(new ErrorResponse("不能将分类移动到自己"));
            if (!repo.MoveCategory(name, body.NewParentName))
            {
                var cat = repo.GetCategory(name);
                if (cat == null) return Results.NotFound(new ErrorResponse("分类不存在"));
                return Results.BadRequest(new ErrorResponse("操作失败：可能存在循环引用"));
            }
            return Results.Ok(new MessageResponse("移动成功"));
        });

        group.MapGet("/{name}/links", (string name, Repository repo) =>
        {
            var category = repo.GetCategory(name);
            if (category is null) return Results.NotFound(new ErrorResponse("分类不存在"));
            return Results.Ok(repo.GetLinks(name));
        });
    }
}
