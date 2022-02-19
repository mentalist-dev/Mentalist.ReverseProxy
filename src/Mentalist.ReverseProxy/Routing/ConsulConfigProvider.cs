using System.Collections.Concurrent;
using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Consul.Models;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Mentalist.ReverseProxy.Routing;

public interface IConsulServiceRegistry: IProxyConfigProvider
{
    void Update(List<ConsulHealthResult> consulServices);
}

public class ConsulEndpoint
{
    public string? Address { get; set; }
    public int? Port { get; set; }
    public bool Healthy { get; set; }
}

public class ConsulConfigProvider: IConsulServiceRegistry
{
    private volatile InMemoryConfig _config = new (new List<RouteConfig>(), new List<ClusterConfig>());

    private readonly ConsulConfiguration _configuration;
    private readonly ConcurrentDictionary<string, Dictionary<string, ConsulEndpoint>> _services = new(StringComparer.OrdinalIgnoreCase);

    public ConsulConfigProvider(ConsulConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Update(List<ConsulHealthResult> consulHealth)
    {
        foreach (var health in consulHealth)
        {
            var consulService = health.Service;

            var tag = consulService.Tags?.FirstOrDefault(tag => tag.StartsWith(_configuration.Tag, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            var path = tag.Substring(_configuration.Tag.Length).Trim();
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (!_services.TryGetValue(path, out var services))
            {
                services = new Dictionary<string, ConsulEndpoint>();
                _services[path] = services;
            }

            var key = $"{consulService.Id}/{consulService.Address}:{consulService.Port}";
            services[key] = new ConsulEndpoint
            {
                Address = consulService.Address,
                Port = consulService.Port,
                Healthy = health.Checks == null || health.Checks.Length == 0 || health.Checks.All(c => c.Status == ConsulConfiguration.Passing)
            };
        }

        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();
        foreach (var item in _services)
        {
            var routeId = item.Key;
            var services = item.Value;

            var path = routeId;
            if (path == "/")
                path = string.Empty;

            var routeConfig = new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = routeId,
                    Match = new RouteMatch
                    {
                        Path = $"{path}/{{*action}}"
                    }
                }
                .WithTransformPathRouteValues(pattern: "/{**action}")
                // this is needed for further call `WithTransformRequestHeader("X-Forwarded-Prefix", routeItem.Prefix, false)`
                .WithTransformXForwarded(xPrefix: ForwardedTransformActions.Set)
                .WithTransformResponseHeaderRemove("x-frame-options", ResponseCondition.Always)
                .WithTransformResponseHeaderRemove("server", ResponseCondition.Always)
                .WithTransformResponseHeaderRemove("x-powered-by", ResponseCondition.Always);

            if (!string.IsNullOrWhiteSpace(path))
            {
                routeConfig = routeConfig.WithTransformRequestHeader("X-Forwarded-Prefix", path, false);
            }

            routes.Add(routeConfig);

            var destinations = new Dictionary<string, DestinationConfig>();

            foreach (var endpoint in services)
            {
                var service = endpoint.Value;
                if (!service.Healthy)
                    continue;

                var address = $"http://{service.Address}";
                if (service.Port > 0)
                {
                    address += $":{service.Port}";
                }

                destinations.Add($"{routeId} - {address}", new DestinationConfig
                {
                    Address = address
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
                Destinations = destinations
            };

            clusters.Add(clusterConfig);
        }

        var oldConfig = _config;
        _config = new InMemoryConfig(routes, clusters);
        oldConfig.Dispose();
    }

    public IProxyConfig GetConfig() => _config;

    private class InMemoryConfig : IProxyConfig, IDisposable
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

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}