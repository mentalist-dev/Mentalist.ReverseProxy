using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Routing;

internal sealed class InMemoryConfig : IProxyConfig, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    ~InMemoryConfig() => Dispose(false);

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}