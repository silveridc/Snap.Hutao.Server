using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace server.Tests.Infrastructure;

public sealed record RouteKey(string Method, string Template);

public static class RegisteredRoutes
{
    public static IReadOnlyList<RouteKey> Enumerate(IServiceProvider services)
    {
        List<RouteKey> routes = [];
        foreach (EndpointDataSource source in services.GetServices<EndpointDataSource>())
        {
            foreach (Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint || routeEndpoint.RoutePattern.RawText is not string raw)
                {
                    continue;
                }

                string template = '/' + raw.TrimStart('/');
                IReadOnlyList<HttpMethodMetadata> methods = routeEndpoint.Metadata.GetOrderedMetadata<HttpMethodMetadata>();

                if (methods.Count is 0)
                {
                    routes.Add(new RouteKey("*", template));
                    continue;
                }

                foreach (HttpMethodMetadata metadata in methods)
                    foreach (string method in metadata.HttpMethods)
                    {
                        routes.Add(new RouteKey(method.ToUpperInvariant(), template));
                    }
            }
        }

        return [.. routes.Distinct()];
    }

    /// <summary>
    /// 判断冒烟清单里的某个具体地址是否命中路由模板：模板参数段（<c>{uid}</c>）匹配任意一段
    /// 具体路径，其余段落大小写不敏感相等；清单地址中的查询串不参与比较.
    /// </summary>
    public static bool Matches(string template, string url)
    {
        string[] templateSegments = Split(template);
        string[] urlSegments = Split(url.Split('?', count: 2)[0]);

        if (templateSegments.Length != urlSegments.Length)
        {
            return false;
        }

        for (int i = 0; i < templateSegments.Length; i++)
        {
            string left = templateSegments[i];
            if (left.StartsWith('{') && left.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(left, urlSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] Split(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }
}