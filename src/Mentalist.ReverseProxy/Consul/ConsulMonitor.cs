using System.Collections.Concurrent;
using System.Text.Json;
using Mentalist.ReverseProxy.Routing;

namespace Mentalist.ReverseProxy.Consul;

public interface IConsulMonitor
{
    void Start(CancellationToken cancellationToken);
}

public class ConsulMonitor: IConsulMonitor
{
    private readonly ConcurrentDictionary<string, ConsulServiceMonitor> _services = new (StringComparer.OrdinalIgnoreCase);

    private readonly string _consulEndpoint;
    private readonly string _consulTag;
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
        catch (TaskCanceledException e)
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