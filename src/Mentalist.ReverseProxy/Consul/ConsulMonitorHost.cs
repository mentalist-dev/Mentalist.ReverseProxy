using Mentalist.ReverseProxy.Settings;

namespace Mentalist.ReverseProxy.Consul;

public class ConsulMonitorHost: IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IConsulMonitor _monitor;
    private readonly IServiceDetailsProvider _service;
    private readonly ILogger<ConsulMonitorHost> _logger;

    public ConsulMonitorHost(IConsulMonitor monitor, IServiceDetailsProvider service, ILogger<ConsulMonitorHost> logger)
    {
        _monitor = monitor;
        _service = service;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _monitor.Start(_cancellationTokenSource.Token);
        await RegisterInConsul(_cancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        await UnRegisterFromConsul();
    }

    private async Task RegisterInConsul(CancellationToken cancellationToken)
    {
        var service = _service.GetInformation();

        if (!string.IsNullOrWhiteSpace(service.Physical.Host))
        {
            var finished = false;
            var delayInSeconds = 0;
            while (!finished)
            {
                try
                {
                    await _monitor.Register(service.Physical, service.Advertised);
                    finished = true;
                }
                catch (Exception e)
                {
                    if (delayInSeconds < 30)
                    {
                        delayInSeconds += 1;
                    }

                    _logger.LogError(e,
                        "Unable to do self registration in consul. Will sleep for {DelayInSeconds} seconds.",
                        delayInSeconds);

                    await Task.Delay(TimeSpan.FromSeconds(delayInSeconds), cancellationToken);
                }
            }
        }
    }

    private async Task UnRegisterFromConsul()
    {
        var service = _service.GetInformation();

        if (!string.IsNullOrWhiteSpace(service.Physical.Host))
        {
            await _monitor.UnRegister(service.Physical);
        }
    }
}