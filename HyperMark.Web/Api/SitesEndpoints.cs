using HyperMark.Storage;
using HyperMark.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// 站点管理端点（/api/sites）
/// </summary>
public static class SitesEndpoints
{
    public static void MapSitesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sites");

        group.MapGet("", (Repository repo) =>
        {
            var sites = repo.GetAllSites();
            return Results.Ok(sites.Select(s => new SiteBrief(s.Name, s.Title)));
        });

        group.MapGet("/{name}", (string name, Repository repo) =>
        {
            var site = repo.GetSite(name);
            return site is null ? Results.NotFound(new ErrorResponse("站点不存在")) : Results.Ok(site);
        });

        group.MapGet("/{name}/full", (string name, Repository repo) =>
        {
            var site = repo.GetSite(name);
            return site is null ? Results.NotFound(new ErrorResponse("站点不存在")) : Results.Ok(site);
        });

        group.MapPost("", async (HttpRequest req, Repository repo) =>
        {
            var site = await req.ReadFromJsonAsync<Site>();
            if (site is null || string.IsNullOrEmpty(site.Name))
                return Results.BadRequest(new ErrorResponse("缺少 name 字段"));

            repo.AddSite(site);
            return Results.Created($"/api/sites/{Uri.EscapeDataString(site.Name)}", site);
        });

        group.MapPut("/{name}", async (string name, HttpRequest req, Repository repo) =>
        {
            var existing = repo.GetSite(name);
            if (existing is null) return Results.NotFound(new ErrorResponse("站点不存在"));

            var updates = await req.ReadFromJsonAsync<SiteUpdateRequest>();
            if (updates is null) return Results.BadRequest(new ErrorResponse("请求体格式错误"));

            if (!string.IsNullOrEmpty(updates.Title)) existing.Title = updates.Title;
            if (!string.IsNullOrEmpty(updates.Homepage)) existing.Homepage = updates.Homepage;
            if (updates.Domains != null) existing.Domains = updates.Domains;
            if (updates.Routes != null) existing.Routes = updates.Routes;
            if (updates.Vars != null) existing.Vars = updates.Vars;

            repo.AddSite(existing);
            return Results.Ok(new MessageResponse("更新成功"));
        });

        group.MapGet("/{name}/domains", (string name, Repository repo) =>
        {
            var site = repo.GetSite(name);
            if (site is null) return Results.NotFound(new ErrorResponse("站点不存在"));
            return Results.Ok(new SiteDomainsResponse(site.Name, site.Domains));
        });

        group.MapPost("/{name}/domains", async (string name, HttpRequest req, Repository repo) =>
        {
            var site = repo.GetSite(name);
            if (site is null) return Results.NotFound(new ErrorResponse("站点不存在"));

            var body = await req.ReadFromJsonAsync<AddDomainRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Domain))
                return Results.BadRequest(new ErrorResponse("缺少 domain 字段"));

            if (!site.Domains.Contains(body.Domain))
            {
                site.Domains.Add(body.Domain);
                repo.AddSite(site);
            }
            return Results.Ok(new MessageResponse("添加成功"));
        });

        group.MapDelete("/{name}/domains/{domain}", (string name, string domain, Repository repo) =>
        {
            var site = repo.GetSite(name);
            if (site is null) return Results.NotFound(new ErrorResponse("站点不存在"));

            if (site.Domains.Remove(domain))
                repo.AddSite(site);

            return Results.Ok(new MessageResponse("删除成功"));
        });

        group.MapGet("/{name}/links", (string name, Repository repo) =>
        {
            var site = repo.GetSite(name);
            if (site is null) return Results.NotFound(new ErrorResponse("站点不存在"));
            return Results.Ok(repo.GetLinksBySite(name));
        });
    }
}
