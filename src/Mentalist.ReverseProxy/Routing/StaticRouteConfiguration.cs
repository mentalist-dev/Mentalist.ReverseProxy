using Mentalist.ReverseProxy.Settings;

namespace Mentalist.ReverseProxy.Routing;

public class StaticRouteConfiguration
{
    public StaticRouteConfiguration(Dictionary<string, ProxyRoute> routes)
    {
        Routes = routes;
    }

    public Dictionary<string, ProxyRoute> Routes { get; }
}