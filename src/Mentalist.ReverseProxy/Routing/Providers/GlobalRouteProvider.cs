using System.Collections.Concurrent;

namespace Mentalist.ReverseProxy.Routing.Providers;

public static class GlobalRouteProvider
{
    private static readonly ConcurrentDictionary<string, string> Routes = new();

    public static bool TryAdd(string routeId, string owner)
    {
        var currentOwner = Routes.GetOrAdd(routeId, owner);
        return currentOwner == owner;
    }
}