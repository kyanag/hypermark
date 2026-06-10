using HyperMark.Models;
using HyperMark.Parsers;
using HyperMark.Storage;

namespace HyperMark.Tests;

public class UrlParserTests
{
    private readonly Repository _repository;
    private readonly UrlParser _parser;

    public UrlParserTests()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "hypermark-tests-" + Guid.NewGuid().ToString("N"));
        _repository = new Repository(new CacheStorage(new LocalStorage(tmpDir)));
        _parser = new UrlParser(_repository);
    }

    #region 域名和站点匹配测试

    [Fact]
    public void Parse_UrlWithDomain_FindsMatchingSite()
    {
        // Arrange
        var site = new Site
        {
            Name = "test-site",
            Title = "Test Site",
            Homepage = "https://example.com/",
            Domains = new List<string> { "example.com", "www.example.com" },
            Routes = new List<Route>
            {
                new Route
                {
                    Name = "home",
                    Title = "Home",
                    Pattern = "/",
                    StdFormat = "/",
                    Args = new List<RouteArg>(),
                    Tags = new List<string> { "@home" }
                }
            }
        };
        _repository.AddSite(site);

        // Act
        var page = _parser.Parse("https://example.com/");

        // Assert
        Assert.NotNull(page);
        Assert.Equal("test-site", page.Site);
        Assert.Equal("home", page.Route);
    }

    [Fact]
    public void Parse_UrlWithQueryParameter_FromQueryArg()
    {
        // Arrange
        var site = new Site
        {
            Name = "search-site",
            Title = "Search Site",
            Homepage = "https://search.example.com/",
            Domains = new List<string> { "search.example.com" },
            Routes = new List<Route>
            {
                new Route
                {
                    Name = "search",
                    Title = "Search",
                    Pattern = "/search",
                    StdFormat = "/search",
                    Args = new List<RouteArg>
                    {
                        new RouteArg
                        {
                            Name = "q",
                            Title = "搜索词",
                            IsRequired = true
                        }
                    },
                    Tags = new List<string> { "@search" }
                }
            }
        };
        _repository.AddSite(site);

        // Act
        var page = _parser.Parse("https://search.example.com/search?q=test%20query");

        // Assert
        Assert.NotNull(page);
        Assert.Equal("search-site", page.Site);
        Assert.Equal("search", page.Route);
        Assert.Contains("q", page.Args.Keys);
        Assert.Equal("test query", page.Args["q"]);
    }

    [Fact]
    public void Parse_UrlWithMixedPathAndQueryArgs()
    {
        // Arrange
        var site = new Site
        {
            Name = "article-site",
            Title = "Article Site",
            Homepage = "https://articles.example.com/",
            Domains = new List<string> { "articles.example.com" },
            Routes = new List<Route>
            {
                new Route
                {
                    Name = "article",
                    Title = "Article",
                    Pattern = "/article/{id}",
                    StdFormat = "/article/{id}",
                    Args = new List<RouteArg>
                    {
                        new RouteArg
                        {
                            Name = "id",
                            Title = "文章 ID",
                            IsRequired = true
                        },
                        new RouteArg
                        {
                            Name = "version",
                            Title = "版本",
                            IsRequired = false,
                            Default = "1"
                        }
                    },
                    Tags = new List<string> { "@article" }
                }
            }
        };
        _repository.AddSite(site);

        // Act
        var page = _parser.Parse("https://articles.example.com/article/123?version=2");

        // Assert
        Assert.NotNull(page);
        Assert.Equal("article-site", page.Site);
        Assert.Equal("article", page.Route);
        Assert.Equal("123", page.Args["id"]);
        Assert.Equal("2", page.Args["version"]);
    }

    #endregion

    #region Query 参数必填检查

    [Fact]
    public void Parse_RequiredQueryArgMissing_TriesNextRoute()
    {
        // Arrange
        var site = new Site
        {
            Name = "filter-site",
            Title = "Filter Site",
            Homepage = "https://filter.example.com/",
            Domains = new List<string> { "filter.example.com" },
            Routes = new List<Route>
            {
                new Route
                {
                    Name = "filter",
                    Title = "Filter",
                    Pattern = "/filter",
                    StdFormat = "/filter",
                    Args = new List<RouteArg>
                    {
                        new RouteArg
                        {
                            Name = "category",
                            Title = "分类",
                            IsRequired = true
                        }
                    },
                    Tags = new List<string> { "@filter" }
                },
                new Route
                {
                    Name = "fallback",
                    Title = "Fallback",
                    Pattern = "/{any}",
                    StdFormat = "/{any}",
                    Args = new List<RouteArg>
                    {
                        new RouteArg
                        {
                            Name = "any",
                            Title = "任意",
                            IsRequired = true
                        }
                    },
                    Tags = new List<string> { "@fallback" }
                }
            }
        };
        _repository.AddSite(site);

        // Act - 没有 category 参数，应该匹配 fallback 路由
        var page = _parser.Parse("https://filter.example.com/other");

        // Assert - 应该匹配 fallback 路由
        Assert.NotNull(page);
        Assert.Equal("fallback", page.Route);
    }

    [Fact]
    public void Parse_RequiredQueryArgProvided_MatchesRoute()
    {
        // Arrange
        var site = new Site
        {
            Name = "category-site",
            Title = "Category Site",
            Homepage = "https://cat.example.com/",
            Domains = new List<string> { "cat.example.com" },
            Routes = new List<Route>
            {
                new Route
                {
                    Name = "category",
                    Title = "Category",
                    Pattern = "/category",
                    StdFormat = "/category",
                    Args = new List<RouteArg>
                    {
                        new RouteArg
                        {
                            Name = "type",
                            Title = "类型",
                            IsRequired = true
                        }
                    },
                    Tags = new List<string> { "@category" }
                }
            }
        };
        _repository.AddSite(site);

        // Act
        var page = _parser.Parse("https://cat.example.com/category?type=books");

        // Assert
        Assert.NotNull(page);
        Assert.Equal("category", page.Route);
        Assert.Equal("books", page.Args["type"]);
    }

    #endregion

    #region 默认值测试

    [Fact]
    public void Parse_QueryArgWithDefault_UsesDefaultValue()
    {
        // Arrange
        var site = new Site
        {
            Name = "default-site",
            Title = "Default Site",
            Homepage = "https://def.example.com/",
            Domains = new List<string> { "def.example.com" },
            Routes = new List<Route>
            {
                new Route
                {
                    Name = "list",
                    Title = "List",
                    Pattern = "/list",
                    StdFormat = "/list",
                    Args = new List<RouteArg>
                    {
                        new RouteArg
                        {
                            Name = "page",
                            Title = "页码",
                            IsRequired = false,
                            Default = "1"
                        }
                    },
                    Tags = new List<string> { "@list" }
                }
            }
        };
        _repository.AddSite(site);

        // Act
        var page = _parser.Parse("https://def.example.com/list");

        // Assert
        Assert.NotNull(page);
        Assert.Equal("list", page.Route);
        Assert.Contains("page", page.Args.Keys);
        Assert.Equal("1", page.Args["page"]);
    }

    #endregion
}
