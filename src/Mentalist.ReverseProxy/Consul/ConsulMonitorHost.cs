using Mentalist.ReverseProxy.Settings;

namespace Mentalist.ReverseProxy.Consul;

public class ConsulMonitorHost: IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IConsulMonitor _monitor;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceDetailsProvider _service;

    public ConsulMonitorHost(IConsulMonitor monitor, IHostApplicationLifetime  lifetime, IServiceDetailsProvider service)
    {
        _monitor = monitor;
        _lifetime = lifetime;
        _service = service;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(RegisterInConsul);
        _lifetime.ApplicationStopping.Register(UnRegisterInConsul);

        _monitor.Start(_cancellationTokenSource.Token);
        
        return Task.CompletedTask;
    }

    private void RegisterInConsul()
    {
        var service = _service.GetInformation();

        if (!string.IsNullOrWhiteSpace(service.Physical.Host))
        {
            var task = _monitor.Register(service.Physical, service.Advertised);
            Task.WaitAll(task);
        }
    }

    private void UnRegisterInConsul()
    {
        var service = _service.GetInformation();

        if (!string.IsNullOrWhiteSpace(service.Physical.Host))
        {
            var task = _monitor.UnRegister(service.Physical);
            Task.WaitAll(task);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }
}