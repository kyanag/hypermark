using HyperMark.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 域名 CNAME 映射管理端点（/api/domain-cnames）
/// </summary>
public static class DomainCnameEndpoints
{
    public static void MapDomainCnameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/domain-cnames");

        group.MapGet("", (Repository repo) =>
        {
            var mappings = repo.GetAllDomainCnames();
            return Results.Ok(mappings.Select(m => new DomainCnameResponse(m.Domain, m.Cname)));
        });

        group.MapPost("", async (HttpRequest req, Repository repo) =>
        {
            var body = await req.ReadFromJsonAsync<AddCnameRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Domain) || string.IsNullOrWhiteSpace(body.Cname))
                return Results.BadRequest(new ErrorResponse("缺少 domain 或 cname 字段"));
            repo.AddDomainCname(body.Domain, body.Cname);
            return Results.Ok(new MessageResponse("添加成功"));
        });

        group.MapDelete("/{domain}", (string domain, Repository repo) =>
        {
            repo.RemoveDomainCname(domain);
            return Results.Ok(new MessageResponse("删除成功"));
        });
    }
}
