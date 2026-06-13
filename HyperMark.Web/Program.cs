using System.Diagnostics;
using HyperMark;
using HyperMark.Config;
using HyperMark.Parsers;
using HyperMark.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#if DEBUG
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.OpenApi;
#endif

namespace HyperMark.Web;

public class WebHost
{
    public static WebApplication CreateApp(string[] args, string? httpUrl = null)
    {
        AppConfig.EnsureInitialized();

        var config = AppConfig.Instance;
        httpUrl ??= config.Http;

        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", httpUrl);

        var storage = new CacheStorage(new LocalStorage());
        var actionLogger = new ActionLogger(storage);
        var fileLogger = new FileLogger();

        // 获取应用程序基础目录
        var baseDir = AppContext.BaseDirectory;
        var webRootPath = Path.Combine(baseDir, "wwwroot");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = webRootPath
        });

#if DEBUG
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "HyperMark API", Version = "v1" });
        });
#endif

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, HyperMarkJsonContext.Default);
        });

        builder.Services.AddSingleton(actionLogger);
        builder.Services.AddSingleton<IStorage>(actionLogger);
        builder.Services.AddSingleton(fileLogger);
        builder.Services.AddSingleton<Repository>(_ => new Repository(actionLogger));
        builder.Services.AddSingleton<UrlParser>(sp => new UrlParser(sp.GetRequiredService<Repository>()));
        builder.Services.AddSingleton<BackgroundLinkProcessor>(sp =>
        {
            var repo = sp.GetRequiredService<Repository>();
            var parser = sp.GetRequiredService<UrlParser>();
            var logger = sp.GetRequiredService<FileLogger>();
            return new BackgroundLinkProcessor(parser, repo, logger);
        });
        builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundLinkProcessor>());

        var app = builder.Build();

        // 全局异常处理中间件
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                fileLogger.LogError($"未处理的请求异常: {context.Request.Method} {context.Request.Path}", ex);
                throw; // 重新抛出让 ASP.NET Core 处理
            }
        });

        // 访问日志中间件
        app.Use(async (context, next) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await next();
            }
            finally
            {
                sw.Stop();
                // 只记录 API 请求，忽略静态文件
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    fileLogger.LogAccess(
                        context.Request.Method,
                        context.Request.Path.ToString(),
                        context.Response.StatusCode,
                        sw.ElapsedMilliseconds);
                }
            }
        });

        // 处理 Private Network Access（从 HTTPS 访问 HTTP localhost）
        app.Use(async (context, next) =>
        {
            // 先让 CORS 中间件处理
            await next();

            // 添加 Private Network Access 头
            if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            }
        });

        app.UseCors();
        app.UseHttpMethodOverride();
        app.UseDefaultFiles();
        app.UseStaticFiles();

#if DEBUG
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HyperMark API v1"));
#endif

        // === 新端点（按 API 设计文档） ===
        app.MapHealthEndpoints();
        app.MapMarkEndpoints();
        app.MapSitesEndpoints();
        app.MapLinksEndpoints();
        app.MapParseEndpoints();
        app.MapCategoriesEndpoints();
        app.MapTagsEndpoints();
        app.MapDomainCnameEndpoints();
        app.MapAdminEndpoints();

        return app;
    }
}
