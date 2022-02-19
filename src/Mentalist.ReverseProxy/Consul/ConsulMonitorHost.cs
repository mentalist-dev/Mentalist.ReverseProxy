namespace Mentalist.ReverseProxy.Consul;

public class ConsulMonitorHost: IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IConsulMonitor _monitor;

    public ConsulMonitorHost(IConsulMonitor monitor)
    {
        _monitor = monitor;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _monitor.Start(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }
}