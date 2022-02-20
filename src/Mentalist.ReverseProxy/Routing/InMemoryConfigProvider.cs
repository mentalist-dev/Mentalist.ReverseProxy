using Mentalist.ReverseProxy.Settings;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Transforms;

namespace Mentalist.ReverseProxy.Routing;

public class InMemoryConfigProvider : IProxyConfigProvider
{
    private volatile InMemoryConfig _config;

    public InMemoryConfigProvider(Dictionary<string, ProxyRoute> proxy)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();
        foreach (var item in proxy)
        {
            var routeId = item.Key;
            var routeItem = item.Value;

            if (routeItem.Path == null)
                continue;

            if (routeItem.Endpoints == null || routeItem.Endpoints.Length == 0)
                continue;

            var routeConfig = new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = routeId,
                    Match = new RouteMatch
                    {
                        Path = $"{routeItem.Path}/{{*action}}"
                    }
                }
                .WithTransformPathRouteValues(pattern: "/{**action}")
                // this is needed for further call `WithTransformRequestHeader("X-Forwarded-Prefix", routeItem.Prefix, false)`
                .WithTransformXForwarded(xPrefix: ForwardedTransformActions.Set)
                .WithTransformUseOriginalHostHeader()
                .WithTransformResponseHeaderRemove("x-frame-options", ResponseCondition.Always)
                .WithTransformResponseHeaderRemove("server", ResponseCondition.Always)
                .WithTransformResponseHeaderRemove("x-powered-by", ResponseCondition.Always);

            if (!string.IsNullOrWhiteSpace(routeItem.Prefix))
            {
                routeConfig = routeConfig.WithTransformRequestHeader("X-Forwarded-Prefix", routeItem.Prefix, false);
            }

            routes.Add(routeConfig);

            HealthCheckConfig? healthCheckConfig = null;
            if (routeItem.HealthCheck?.Enabled == true)
            {
                var healthCheckPath = routeItem.HealthCheck.Path ?? "/";
                healthCheckConfig = new HealthCheckConfig
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

            var destinations = new Dictionary<string, DestinationConfig>();
            for (var i = 0; i < routeItem.Endpoints.Length; i++)
            {
                var endpoint = routeItem.Endpoints[i];
                destinations.Add($"{routeId}/{endpoint}", new DestinationConfig
                {
                    Address = endpoint
                });
            }

            var clusterConfig = new ClusterConfig
            {
                ClusterId = routeId,
                LoadBalancingPolicy = "PowerOfTwoChoices",
                SessionAffinity = new SessionAffinityConfig
                {
                    Enabled = false
                },
                HealthCheck = healthCheckConfig,
                Destinations = destinations
            };

            clusters.Add(clusterConfig);
        }

        _config = new InMemoryConfig(routes, clusters);
    }

    public IProxyConfig GetConfig() => _config;

    // public void Update(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
    // {
    //     var oldConfig = _config;
    //     _config = new InMemoryConfig(routes, clusters);
    //     oldConfig.SignalChange();
    // }

    private class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }

        internal void SignalChange()
        {
            _cts.Cancel();
        }
    }
}