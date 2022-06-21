using System.Collections.Concurrent;
using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Consul.Models;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Routing.Providers;

public interface IConsulServiceRegistry : IProxyConfigProvider
{
    void Update(string serviceName, List<ConsulHealthResult> consulHealth);
}

public class ConsulConfigProvider : RouteConfigProvider, IConsulServiceRegistry
{
    private volatile InMemoryConfig _config = new(new List<RouteConfig>(), new List<ClusterConfig>());

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, ServiceContainer> _services = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConsulConfiguration _configuration;

    public IProxyConfig GetConfig() => _config;

    public ConsulConfigProvider(RoutingConfiguration routing, ConsulConfiguration configuration) : base(routing)
    {
        _configuration = configuration;
    }

    public void Update(string serviceName, List<ConsulHealthResult> consulHealth)
    {
        var serviceContainer = CreateServiceContainer(serviceName, consulHealth);

        lock (_lock)
        {
            if (!_services.TryGetValue(serviceName, out var sc))
            {
                sc = serviceContainer;
                _services[serviceName] = sc;
            }
            else
            {
                // this logic ensures that route is never removed and in case of 0 destinations - 503 is returned

                // add/update existing destinations
                foreach (var pair in serviceContainer.Paths)
                {
                    sc.Paths[pair.Key] = pair.Value;
                }

                // clear missing
                foreach (var pair in sc.Paths)
                {
                    if (!serviceContainer.Paths.ContainsKey(pair.Key))
                    {
                        pair.Value.Endpoints.Clear();
                    }
                }
            }

            // rebuild proxy routes
            var routeMap = new Dictionary<string, RouteConfig>();
            var destinations = new Dictionary<string, Dictionary<string, DestinationConfig>>();

            BuildDynamicRoutes(routeMap, destinations);

            var routes = routeMap.Values.ToList();
            var clusters = destinations.Select(d => new ClusterConfig
            {
                ClusterId = d.Key,
                LoadBalancingPolicy = "PowerOfTwoChoices",
                SessionAffinity = new SessionAffinityConfig
                {
                    Enabled = false
                },
                Destinations = d.Value
            }).ToList();

            var oldConfig = _config;
            _config = new InMemoryConfig(routes, clusters);
            oldConfig.Dispose();
        }
    }

    private void BuildDynamicRoutes(Dictionary<string, RouteConfig> routeMap, Dictionary<string, Dictionary<string, DestinationConfig>> destinations)
    {
        foreach (var servicePath in _services.Values.SelectMany(c => c.Paths.Values))
        {
            var routeId = servicePath.Path;
            var path = routeId;
            if (path == "/")
                path = string.Empty;

            if (!GlobalRouteProvider.TryAdd(routeId, GetType().Name))
                continue;

            if (!routeMap.ContainsKey(routeId))
            {
                var routeConfig = CreateRouteConfig(routeId, path);
                routeMap.Add(routeId, routeConfig);
            }

            if (!destinations.TryGetValue(routeId, out var destination))
            {
                destination = new Dictionary<string, DestinationConfig>();
                destinations[routeId] = destination;
            }

            foreach (var endpoint in servicePath.Endpoints.Values)
            {
                if (!endpoint.Healthy)
                    continue;

                var address = $"http://{endpoint.Address}";
                if (endpoint.Port > 0)
                {
                    address += $":{endpoint.Port}";
                }

                destination[$"{routeId} - {address}"] = new DestinationConfig
                {
                    Address = address,
                    Metadata = new Dictionary<string, string>
                    {
                        {"Timestamp", DateTime.UtcNow.ToString("O")}
                    }
                };
            }
        }
    }

    private ServiceContainer CreateServiceContainer(string serviceName, List<ConsulHealthResult> consulHealth)
    {
        var serviceContainer = new ServiceContainer(serviceName);
        foreach (var health in consulHealth)
        {
            var consulService = health.Service;

            var tag = consulService.Tags?.FirstOrDefault(tag => tag.StartsWith(_configuration.Tag, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            var path = tag.Substring(_configuration.Tag.Length).Trim();
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (!serviceContainer.Paths.TryGetValue(path, out var endpointContainer))
            {
                endpointContainer = new PathContainer(path);
                serviceContainer.Paths[path] = endpointContainer;
            }

            var key = $"{consulService.Id}/{consulService.Address}/{consulService.Port}";
            endpointContainer.Endpoints[key] = new ConsulEndpoint
            {
                Address = consulService.Address,
                Port = consulService.Port,
                Healthy = health.Checks == null ||
                          health.Checks.Length == 0 ||
                          health.Checks.All(c => c.Status == ConsulConfiguration.Passing)
            };
        }

        return serviceContainer;
    }

    private sealed class ServiceContainer
    {
        public ServiceContainer(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; }
        public Dictionary<string, PathContainer> Paths { get; } = new();
    }

    private sealed class PathContainer
    {
        public PathContainer(string path)
        {
            Path = path;
        }

        public string Path { get; }
        public Dictionary<string, ConsulEndpoint> Endpoints { get; } = new();
    }
}