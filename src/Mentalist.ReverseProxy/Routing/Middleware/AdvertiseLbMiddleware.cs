using System.Diagnostics;
using System.Reflection;
using Mentalist.ReverseProxy.LogzIo;
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

            var serviceHost = _serviceInformation.Advertised.Host;
            if (_serviceInformation.Advertised.Port != 80 && _serviceInformation.Advertised.Port != 443)
            {
                serviceHost += $":{_serviceInformation.Advertised.Port}";
            }

            context.Response.Headers.Add("lb-host", serviceHost);
            context.Response.Headers.Add("lb-ver", AssemblyVersion);

            var activity = Activity.Current;
            if (activity != null)
            {
                var traceId = activity.GetTraceId();
                if (!string.IsNullOrWhiteSpace(traceId))
                {
                    context.Response.Headers.Add("lb-trace-id", traceId);
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}