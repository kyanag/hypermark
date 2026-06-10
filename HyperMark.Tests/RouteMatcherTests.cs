using HyperMark.Matching;
using HyperMark.Models;

namespace HyperMark.Tests;

public class RouteMatcherTests
{
    #region Match 方法测试 - 成功场景

    [Fact]
    public void Match_SimplePath_MatchesCorrectly()
    {
        // Arrange
        var route = new Route
        {
            Name = "category",
            Pattern = "/acting/{category_id}",
            StdFormat = "/acting/{category_id}",
            Args = new List<RouteArg>()
        };
        var path = "/acting/solo";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.NotNull(args);
        Assert.Single(args);
        Assert.Equal("solo", args["category_id"]);
    }

    [Fact]
    public void Match_MultipleParams_ExtractsAllParams()
    {
        // Arrange
        var route = new Route
        {
            Name = "song",
            Pattern = "/music/{genre_id}/song/{song_id}",
            StdFormat = "/music/{genre_id}/song/{song_id}",
            Args = new List<RouteArg>()
        };
        var path = "/music/pop/song/123";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.NotNull(args);
        Assert.Equal(2, args.Count);
        Assert.Equal("pop", args["genre_id"]);
        Assert.Equal("123", args["song_id"]);
    }

    [Fact]
    public void Match_PathWithEncodedChars_DecodesCorrectly()
    {
        // Arrange
        var route = new Route
        {
            Name = "music",
            Pattern = "/music/{song_name}",
            StdFormat = "/music/{song_name}",
            Args = new List<RouteArg>()
        };
        var path = "/music/Dream%20It%20Possible";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("Dream It Possible", args["song_name"]);
    }

    [Fact]
    public void Match_RootPath_MatchesEmpty()
    {
        // Arrange
        var route = new Route
        {
            Name = "home",
            Pattern = "/",
            StdFormat = "/",
            Args = new List<RouteArg>()
        };
        var path = "/";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.NotNull(args);
        Assert.Empty(args);
    }

    [Fact]
    public void Match_StaticPath_MatchesWithoutParams()
    {
        // Arrange
        var route = new Route
        {
            Name = "about",
            Pattern = "/about",
            StdFormat = "/about",
            Args = new List<RouteArg>()
        };
        var path = "/about";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.NotNull(args);
        Assert.Empty(args);
    }

    #endregion

    #region Match 方法测试 - 失败场景

    [Fact]
    public void Match_DifferentPath_ReturnsNull()
    {
        // Arrange
        var route = new Route
        {
            Name = "category",
            Pattern = "/acting/{category_id}",
            StdFormat = "/acting/{category_id}",
            Args = new List<RouteArg>()
        };
        var path = "/music/solo";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.Null(args);
    }

    [Fact]
    public void Match_PartialPath_ReturnsNull()
    {
        // Arrange
        var route = new Route
        {
            Name = "category",
            Pattern = "/acting/{category_id}",
            StdFormat = "/acting/{category_id}",
            Args = new List<RouteArg>()
        };
        var path = "/acting";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.Null(args);
    }

    #endregion

    #region RoundTrip 测试

    [Fact]
    public void RoundTrip_MatchThenBuild_ProducesSamePath()
    {
        // Arrange
        var route = new Route
        {
            Name = "category",
            Pattern = "/acting/{category_id}",
            StdFormat = "/acting/{category_id}",
            Args = new List<RouteArg>()
        };
        var path = "/acting/solo";

        // Act
        var args = RouteMatcher.Match(route, path);
        var rebuiltPath = RouteMatcher.BuildStdUrl(route, args!);

        // Assert
        Assert.Equal(path, rebuiltPath);
    }

    [Fact]
    public void RoundTrip_MultipleParams_MatchThenBuild_ProducesSamePath()
    {
        // Arrange
        var route = new Route
        {
            Name = "song",
            Pattern = "/music/{genre_id}/song/{song_id}",
            StdFormat = "/music/{genre_id}/song/{song_id}",
            Args = new List<RouteArg>()
        };
        var path = "/music/pop/song/123";

        // Act
        var args = RouteMatcher.Match(route, path);
        var rebuiltPath = RouteMatcher.BuildStdUrl(route, args!);

        // Assert
        Assert.Equal(path, rebuiltPath);
    }

    #endregion

    #region 完整 URL 测试

    [Fact]
    public void Match_FullUrl_ExtractsPathAndMatches()
    {
        // Arrange
        var route = new Route
        {
            Name = "category",
            Pattern = "/acting/{category_id}",
            StdFormat = "/acting/{category_id}",
            Args = new List<RouteArg>()
        };
        var url = "https://example.com/acting/solo";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Single(args);
        Assert.Equal("solo", args["category_id"]);
    }

    #endregion

    #region 特殊字符测试

    [Fact]
    public void Match_ChineseCharacters_DecodesCorrectly()
    {
        // Arrange
        var route = new Route
        {
            Name = "music",
            Pattern = "/music/{song_name}",
            StdFormat = "/music/{song_name}",
            Args = new List<RouteArg>()
        };
        var path = "/music/最炫民族风";

        // Act
        var args = RouteMatcher.Match(route, path);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("最炫民族风", args["song_name"]);
    }

    #endregion

    #region PathStyle 模式测试

    [Fact]
    public void Match_PathStyle_BasicMatch()
    {
        // Arrange
        var route = new Route
        {
            Name = "thread",
            Pattern = "/read.php?tid-{tid}-keyword-{keyword}.html",
            StdFormat = "/thread-{tid}.html",
            Args = new List<RouteArg>()
        };
        var url = "/read.php?tid-2627604-keyword-张三.html";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Equal(2, args.Count);
        Assert.Equal("2627604", args["tid"]);
        Assert.Equal("张三", args["keyword"]);
    }

    [Fact]
    public void Match_PathStyle_AutoInferred_NoEquals()
    {
        // Arrange - pattern 中没有 =，自动推断为 PathStyle
        var route = new Route
        {
            Name = "article",
            Pattern = "/page?id-{id}-type-{type}",
            StdFormat = "/article/{id}",
            Args = new List<RouteArg>()
            // 故意不设置 QueryMode，测试自动推断
        };
        var url = "/page?id-123-type-news";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("123", args["id"]);
        Assert.Equal("news", args["type"]);
    }

    [Fact]
    public void Match_PathStyle_ExplicitMode()
    {
        // Arrange - 显式设置为 PathStyle
        var route = new Route
        {
            Name = "custom",
            Pattern = "/search?keyword-{keyword}",
            StdFormat = "/search/{keyword}",
            QueryMode = QueryParseMode.PathStyle,
            Args = new List<RouteArg>()
        };
        var url = "/search?keyword-test";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("test", args["keyword"]);
    }

    [Fact]
    public void Match_StandardMode_StillWorks()
    {
        // Arrange - 确保标准模式不受影响
        var route = new Route
        {
            Name = "forum",
            Pattern = "/forum.php?mod=viewthread&tid={tid}",
            StdFormat = "/thread-{tid}.html",
            QueryMode = QueryParseMode.Standard, // 显式设置
            Args = new List<RouteArg>()
        };
        var url = "/forum.php?mod=viewthread&tid=12345";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("12345", args["tid"]);
    }

    [Fact]
    public void Match_PathStyle_WithChineseChars()
    {
        // Arrange
        var route = new Route
        {
            Name = "search",
            Pattern = "/search?q-{q}-category-{category}",
            StdFormat = "/search/{q}",
            Args = new List<RouteArg>()
        };
        var url = "/search?q-张三-category-用户";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("张三", args["q"]);
        Assert.Equal("用户", args["category"]);
    }

    [Fact]
    public void Match_PathStyle_NoMatch()
    {
        // Arrange
        var route = new Route
        {
            Name = "thread",
            Pattern = "/read.php?tid-{tid}-keyword-{keyword}.html",
            StdFormat = "/thread-{tid}.html",
            Args = new List<RouteArg>()
        };
        var url = "/other.php?tid-123-keyword-test.html"; // 路径不匹配

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.Null(args);
    }

    [Fact]
    public void Match_PathStyle_FullUrl()
    {
        // Arrange
        var route = new Route
        {
            Name = "thread",
            Pattern = "/read.php?tid-{tid}-keyword-{keyword}.html",
            StdFormat = "/thread-{tid}.html",
            Args = new List<RouteArg>()
        };
        var url = "https://example.com/read.php?tid-2627604-keyword-张三.html";

        // Act
        var args = RouteMatcher.Match(route, url);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("2627604", args["tid"]);
        Assert.Equal("张三", args["keyword"]);
    }

    #endregion

    #region 我的测试场景 (to ai: 修改代码时不要给这一段 region 删除)

    [Fact]
    public void Match_My_Cases()
    {
        // Arrange
        var route = new Route
        {
            Name = "discuz.thread",
            Pattern = "/forum.php?mod=viewthread&tid={thread_id}",
            StdFormat = "/thread-{thread_id}-1.html",
            Args = new List<RouteArg>()
            {
                new RouteArg()
                {
                    Title = "帖子 id",
                    Name = "thread_id",
                    IsRequired = true,
                },
                new RouteArg()
                {
                    Title = "页数",
                    Name = "page",
                    Default = "1",
                }
            }
        };
        var originalPath = "/forum.php?mod=viewthread&tid=12345";

        // Act
        var args = RouteMatcher.Match(route, originalPath);
        var rebuiltPath = RouteMatcher.BuildStdUrl(route, args!);

        // Assert
        Assert.NotNull(args);
        Assert.Equal("12345", args["thread_id"]);
        Assert.Single(args);
        Assert.Equal("/thread-12345-1.html", rebuiltPath);


        var originalPath2 = "/forum.php?tid=12345&mod=viewthread";

        // Act
        var args2 = RouteMatcher.Match(route, originalPath2);
        var rebuiltPath2 = RouteMatcher.BuildStdUrl(route, args2!);

        // Assert
        Assert.NotNull(args2);
        Assert.Equal("12345", args2["thread_id"]);
        Assert.Single(args2);
        Assert.Equal("/thread-12345-1.html", rebuiltPath2);
    }
    #endregion

}
