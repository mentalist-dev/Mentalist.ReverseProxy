using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Mentalist.ReverseProxy.Routing.Providers;

public class RouteConfigProvider
{
    private readonly RoutingConfiguration _routing;

    public RouteConfigProvider(RoutingConfiguration routing)
    {
        _routing = routing;
    }

    protected RouteConfig CreateRouteConfig(string routeId, string path, string? prefix = null, bool useOriginalHost = true)
    {
        var routeConfig = new RouteConfig
            {
                RouteId = routeId,
                ClusterId = routeId,
                Match = new RouteMatch
                {
                    Path = $"{path}/{{**action}}"
                }
            }
            // .WithTransformPathRouteValues(pattern: "/{**action}")
            // this is needed for further call `WithTransformRequestHeader("X-Forwarded-Prefix", routeItem.Prefix, false)`
            .WithTransformXForwarded(xPrefix: ForwardedTransformActions.Set, xFor: ForwardedTransformActions.Append)
            .WithTransformResponseHeaderRemove("Server", ResponseCondition.Always)
            .WithTransformResponseHeaderRemove("X-Powered-By", ResponseCondition.Always)
            .WithTransformResponseHeader("X-XSS-Protection", "1; mode=block", true, ResponseCondition.Always)
            .WithTransformResponseHeader("X-Content-Type-Options", "nosniff", true, ResponseCondition.Always)
            .WithTransformResponseHeader("Referrer-Policy", "origin", true, ResponseCondition.Always);

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            routeConfig = routeConfig
                .WithTransformPathRouteValues("/{**action}")
                ;
        }

        if (useOriginalHost)
        {
            routeConfig = routeConfig.WithTransformUseOriginalHostHeader();
        }

        var allowedXFrameOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DENY", "SAMEORIGIN" };
        if (!string.IsNullOrWhiteSpace(_routing.XFrameOptions) && allowedXFrameOptions.Contains(_routing.XFrameOptions))
        {
            routeConfig.WithTransformResponseHeader("X-Frame-Options", _routing.XFrameOptions, true, ResponseCondition.Always);
        }

        if (_routing.ForceHttps && _routing.EnableHsts)
        {
            routeConfig.WithTransformResponseHeader("Strict-Transport-Security", "max-age=31536001; includeSubDomains; preload", true, ResponseCondition.Always);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            routeConfig = routeConfig.WithTransformRequestHeader("X-Forwarded-Prefix", path, false);
        }

        return routeConfig;
    }
}