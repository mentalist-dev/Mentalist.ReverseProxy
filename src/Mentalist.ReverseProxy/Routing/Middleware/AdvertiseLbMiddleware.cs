using System.Reflection;
using Mentalist.ReverseProxy.Settings;

namespace Mentalist.ReverseProxy.Routing.Middleware;

public class AdvertiseLbMiddleware
{
    private static readonly string AssemblyVersion = Assembly.GetAssembly(typeof(AdvertiseLbMiddleware))?.GetName().Version?.ToString() ?? "0.0.0.0";

    private readonly RequestDelegate _next;
    private readonly IServiceDetailsProvider _service;

    private ServiceInformation? _serviceInformation;

    public AdvertiseLbMiddleware(RequestDelegate next, IServiceDetailsProvider service)
    {
        _next = next;
        _service = service;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Response.HasStarted)
        {
            _serviceInformation ??= _service.GetInformation();

            context.Response.Headers.Add("mentalist-lb-host", $"{_serviceInformation.Advertised.Host}:{_serviceInformation.Advertised.Port}");
            context.Response.Headers.Add("mentalist-lb-ver", AssemblyVersion);
        }

        await _next(context).ConfigureAwait(false);
    }
}