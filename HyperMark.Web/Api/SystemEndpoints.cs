using HyperMark.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 系统端点：健康检查 + replay
/// </summary>
public static class SystemEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", VersionConstants.Version)));
        app.MapPost("/api/replay/links", (ActionLogger logger) =>
        {
            var count = logger.ReplayLinks();
            return Results.Ok(new MessageResponse($"重建完成，恢复 {count} 条链接"));
        });
    }
}
