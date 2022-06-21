using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

namespace Mentalist.ReverseProxy.Routing.Providers;

public class StaticRouteConfigProvider : RouteConfigProvider, IProxyConfigProvider
{
    private readonly InMemoryConfig _config;

    public IProxyConfig GetConfig() => _config;

    public StaticRouteConfigProvider(RoutingConfiguration routing, StaticRouteConfiguration configuration) : base(routing)
    {
        var proxyRoutes = configuration.Routes;

        var routeMap = new Dictionary<string, RouteConfig>();
        var destinations = new Dictionary<string, Destination>();

        foreach (var route in proxyRoutes.Select(r => r.Value))
        {
            if (route.Path == null || route.Endpoints == null || route.Endpoints.Length == 0)
                continue;

            var routeId = route.Path;
            if (string.IsNullOrWhiteSpace(routeId))
                routeId = "/";

            routeId = $"{routeId}";

            var path = route.Path;
            if (path == "/")
                path = string.Empty;

            if (!GlobalRouteProvider.TryAdd(routeId, GetType().Name))
                continue;

            if (!routeMap.ContainsKey(routeId))
            {
                var routeConfig = CreateRouteConfig(routeId, path, route.UseOriginalHost);
                routeMap.Add(routeId, routeConfig);
            }

            if (!destinations.TryGetValue(routeId, out var destination))
            {
                destination = new Destination();
                destinations[routeId] = destination;
            }

            if (route.HealthCheck?.Enabled == true)
            {
                var healthCheckPath = route.HealthCheck.Path ?? "/";
                destination.HealthCheckConfig = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Interval = TimeSpan.FromSeconds(10),
                        Timeout = TimeSpan.FromSeconds(10),
                        Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures,
                        Path = healthCheckPath
                    }
                };
            }

            foreach (var address in route.Endpoints)
            {
                destination.DestinationConfig[$"{routeId} - {address}"] = new DestinationConfig
                {
                    Address = address,
                    Metadata = new Dictionary<string, string>
                    {
                        {"Timestamp", DateTime.UtcNow.ToString("O")}
                    }
                };
            }
        }

        var routes = routeMap.Values.ToList();
        var clusters = destinations.Select(d => new ClusterConfig
        {
            ClusterId = d.Key,
            LoadBalancingPolicy = "PowerOfTwoChoices",
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = false
            },
            HealthCheck = d.Value.HealthCheckConfig,
            Destinations = d.Value.DestinationConfig
        }).ToList();

        _config = new InMemoryConfig(routes, clusters);
    }

    private sealed class Destination
    {
        public HealthCheckConfig? HealthCheckConfig { get; set; }

        public Dictionary<string, DestinationConfig> DestinationConfig { get; } = new();
    }
}