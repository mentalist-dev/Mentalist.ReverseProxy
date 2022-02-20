using System.Text.Json;
using Mentalist.ReverseProxy.Consul.Models;
using Mentalist.ReverseProxy.Routing;

namespace Mentalist.ReverseProxy.Consul;

public class ConsulServiceMonitor : IDisposable
{
    private readonly string _serviceName;
    private readonly ILogger _logger;
    private readonly IConsulServiceRegistry _consulServiceMonitor;

    private readonly string _consulEndpoint;
    private readonly HttpClient _httpClient;
    private long _serviceIndex;
    private bool _isDisposed;

    public ConsulServiceMonitor(string serviceName, string consulEndpoint, IConsulServiceRegistry consulServiceMonitor, HttpClient httpClient, ILogger logger)
    {
        _serviceName = serviceName;
        _consulEndpoint = consulEndpoint;
        _httpClient = httpClient;
        _logger = logger;
        _consulServiceMonitor = consulServiceMonitor;
    }

    public bool IsRunning { get; private set; }

    public void Dispose()
    {
        _isDisposed = true;
    }

    public void Start(CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(() => Monitor(cancellationToken), TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
    }

    private async Task Monitor(CancellationToken cancellationToken)
    {
        try
        {
            IsRunning = true;

            while (!_isDisposed && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var url = $"{_consulEndpoint}/v1/health/service/{_serviceName}?index={_serviceIndex}";
                    using var response = await _httpClient.GetAsync(url, cancellationToken);

                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError(
                            "Unable to get service list from consul. Response status code = {ResponseStatusCode}. Response content = {ResponseBody}",
                            response.StatusCode.ToString(),
                            responseContent
                        );

                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var health = JsonSerializer.Deserialize<List<ConsulHealthResult>>(responseContent);
                        _serviceIndex = response.GetConsulIndex(_serviceIndex);

                        if (health != null)
                        {
                            _consulServiceMonitor.Update(_serviceName, health);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Service {ServiceName} monitoring loop exited", _serviceName);
        }
        finally
        {
            IsRunning = false;
        }
    }
}
