using System.Collections.Concurrent;
using System.Text.Json;
using Mentalist.ReverseProxy.Routing.Providers;
using Mentalist.ReverseProxy.Settings;

namespace Mentalist.ReverseProxy.Consul;

public interface IConsulMonitor
{
    void Start(CancellationToken cancellationToken);
    Task Register(ServiceAddress physical, ServiceAddress advertised);
    Task UnRegister(ServiceAddress physical);
}

public class ConsulMonitor: IConsulMonitor
{
    private readonly ConcurrentDictionary<string, ConsulServiceMonitor> _services = new (StringComparer.OrdinalIgnoreCase);

    private readonly string _consulEndpoint;
    private readonly string _consulTag;
    private readonly string _serviceName;
    private readonly HttpClient _httpClient;
    private readonly IConsulServiceRegistry _consulServiceMonitor;
    private readonly ILogger<ConsulMonitor> _logger;

    private long _serviceIndex;

    public ConsulMonitor(ConsulConfiguration configuration, IConsulServiceRegistry consulServiceMonitor, HttpClient httpClient, ILogger<ConsulMonitor> logger)
    {
        _consulEndpoint = configuration.Endpoint.TrimEnd('/');

        _consulTag = configuration.Tag;
        if (string.IsNullOrWhiteSpace(_consulTag))
            _consulTag = ConsulConfiguration.DefaultTag;

        _serviceName = configuration.ServiceName;
        if (string.IsNullOrWhiteSpace(_serviceName))
            _serviceName = ConsulConfiguration.DefaultServiceName;

        _consulServiceMonitor = consulServiceMonitor;
        _httpClient = httpClient;
        _logger = logger;
    }

    public void Start(CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(
            () => Monitor(cancellationToken),
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach
        );
    }

    public async Task Register(ServiceAddress physical, ServiceAddress advertised)
    {
        var serviceId = CreateServiceId(_serviceName, physical.Host, physical.Port);

        var address = advertised.Host;
        var port = advertised.Port;

        var request = new
        {
            ID = serviceId,
            Name = _serviceName,
            Tags = new[] {"metrics"},
            Address = address,
            Port = port,
            Checks = new[]
            {
                new
                {
                    HTTP = $"http://{physical.Host}:{physical.Port}/status",
                    Method = "GET",
                    Interval = "10s",
                    Timeout = "30s"
                }
            }
        };

        using var response = await _httpClient
            .PutAsync($"{_consulEndpoint}/v1/agent/service/register", new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(request)))
            .ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("Registered in consul. Consul response: [{ResponseStatusCode}] {ResponseBody}", response.StatusCode, responseBody);
    }

    public async Task UnRegister(ServiceAddress physical)
    {
        var serviceId = CreateServiceId(_serviceName, physical.Host, physical.Port);

        using var response = await _httpClient
            .PutAsync($"{_consulEndpoint}/v1/agent/service/deregister/{serviceId}", new StringContent(string.Empty))
            .ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("Unregistered from consul. Consul response: [{ResponseStatusCode}] {ResponseBody}", response.StatusCode, responseBody);
    }

    private static string CreateServiceId(string serviceName, string host, int port)
    {
        var hashCode = $"{host}:{port}".GetHashCode();
        var serviceId = $"{hashCode:X}@{serviceName}".ToLower();
        return serviceId;
    }

    private async Task Monitor(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Entered consul monitor thread. Consul endpoint = {ConsulEndpoint}. Consul tag = {ConsulTag}.",
            _consulEndpoint, _consulTag);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var services = await GetServices(cancellationToken).ConfigureAwait(false);

                if (services == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var service in services)
                {
                    var serviceName = service.Key;
                    var serviceTags = service.Value;

                    if (serviceTags.Any(s => s.StartsWith(_consulTag, StringComparison.OrdinalIgnoreCase)))
                    {
                        _services.TryGetValue(serviceName, out var serviceMonitor);

                        if (serviceMonitor == null || !serviceMonitor.IsRunning)
                        {
                            serviceMonitor?.Dispose();

                            serviceMonitor = new ConsulServiceMonitor(serviceName, _consulEndpoint, _consulServiceMonitor, _httpClient, _logger);
                            _services[serviceName] = serviceMonitor;
                            serviceMonitor.Start(cancellationToken);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogError(e, "Consul monitor loop canceled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Consul monitor loop exception");
        }
        finally
        {
            _logger.LogWarning("Exiting consul monitor thread");
        }
    }

    private async Task<Dictionary<string, string[]>?> GetServices(CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_consulEndpoint}/v1/catalog/services?index={_serviceIndex}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Unable to get service list from consul. Response status code = {ResponseStatusCode}. Response content = {ResponseBody}",
                    response.StatusCode.ToString(),
                    responseContent
                );

                return null;
            }

            var services = JsonSerializer.Deserialize<Dictionary<string, string[]>>(responseContent);
            _serviceIndex = response.GetConsulIndex(_serviceIndex);

            return services ?? new Dictionary<string, string[]>();
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to get service list from consul.");
            return null;
        }
    }
}