using HyperMark.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 管理端删除端点（/admin）
/// 破坏性操作（删除站点/分类/Tag），仅限管理后台调用
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin");

        group.MapDelete("/sites/{name}", (string name, Repository repo) =>
        {
            if (repo.GetSite(name) is null)
                return Results.NotFound(new ErrorResponse("站点不存在"));
            repo.DeleteSite(name);
            return Results.Ok(new MessageResponse("删除成功"));
        });

        group.MapDelete("/categories/{name}", (string name, bool force, Repository repo) =>
        {
            try
            {
                if (!repo.DeleteCategory(name, force))
                    return Results.NotFound(new ErrorResponse("分类不存在"));
                return Results.Ok(new MessageResponse("删除成功"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message));
            }
        });

        group.MapDelete("/tags/{id}", (int id, Repository repo) =>
        {
            if (!repo.DeleteTag(id))
                return Results.NotFound(new ErrorResponse("标签不存在"));
            return Results.Ok(new MessageResponse("删除成功"));
        });

        group.MapPost("/replay/links", (ActionLogger logger) =>
        {
            var count = logger.ReplayLinks();
            return Results.Ok(new MessageResponse($"重建完成，恢复 {count} 条链接"));
        });
    }
}
