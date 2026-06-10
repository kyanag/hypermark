using HyperMark.Storage;
using HyperMark.Matching;

namespace HyperMark.Tests;

public class UnitTest1
{
    [Fact]
    public void Test_98Tang()
    {
        var storage = new CacheStorage(new LocalStorage(".hypermark"));
        var site = storage.GetSite("98tang");

        var routes = site?.Routes;
        if(routes != null)
        {
            var path_route = routes[0];
            var query_route = routes[1];

            var query_url = "https://www.sehuatang.net/forum.php?mod=forumdisplay&fid=2&filter=lastpost&orderby=lastpost";

            var args = RouteMatcher.Match(query_route, query_url);
            var std_url = RouteMatcher.BuildStdUrl(query_route, args);

            var path_url = "https://www.sehuatang.net/forum-2-1.html";
            var args2 = RouteMatcher.Match(path_route, path_url);
            var std_url2 = RouteMatcher.BuildStdUrl(path_route, args2);
        }
    }
}
