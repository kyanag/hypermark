using HyperMark.Parsers;
using HyperMark.Storage;
using HyperMark.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HyperMark.Web;

/// <summary>
/// URL 解析端点（/api/parse）
/// </summary>
public static class ParseEndpoints
{
    public static void MapParseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/parse");

        group.MapGet("", (string url, Repository repo, UrlParser parser) =>
        {
            var page = parser.Parse(url);
            var link = repo.GetLinkByUrl(url);

            var pageInfo = new ParsePageInfo(page.HyperId, page.Site, page.Std, page.Args, page.Tags, page.Route);

            if (link != null)
            {
                var linkTags = repo.GetLinkTags(url);
                return Results.Ok(new ParseResultResponse(pageInfo,
                    new ParseLinkInfo(link.Url, link.Title, link.Category, link.CreatedAt, linkTags.Select(t => t.Name).ToList())));
            }

            return Results.Ok(new ParseResultResponse(pageInfo, null));
        });

        group.MapGet("/match", (string url, UrlParser parser) =>
        {
            var page = parser.Parse(url);
            var matched = !string.IsNullOrEmpty(page.Site);
            return Results.Ok(new ParseMatchResponse(
                matched,
                matched ? page.Site : null,
                matched && !string.IsNullOrEmpty(page.Route) ? page.Route : null));
        });

        group.MapPost("/batch", async (HttpRequest req, Repository repo, UrlParser parser, FileLogger logger) =>
        {
            BatchParseRequestBody? body = null;
            try { body = await req.ReadFromJsonAsync<BatchParseRequestBody>(); }
            catch (Exception ex) { logger.LogError(ex, "反序列化 batch 请求体失败"); }

            List<Link> links;
            if (body?.Urls is { Count: > 0 })
                links = body.Urls.Select(u => repo.GetLinkByUrl(u)).Where(l => l != null).ToList()!;
            else
                links = repo.GetLinks().Where(l => l.Page is null).ToList();

            int success = 0, failed = 0;
            foreach (var link in links)
            {
                try
                {
                    var page = parser.Parse(link.Url);
                    if (!string.IsNullOrEmpty(page.HyperId)) { repo.UpdateLinkPage(link.Url, page); success++; }
                    else failed++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"解析链接失败: {link.Url}");
                    failed++;
                }
            }

            return Results.Ok(new ParseBatchResponse("解析完成", links.Count, success, failed));
        });
    }
}
