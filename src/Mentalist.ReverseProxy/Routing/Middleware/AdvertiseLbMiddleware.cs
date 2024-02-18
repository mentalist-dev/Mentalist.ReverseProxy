using System.Diagnostics;
using System.Reflection;
using Mentalist.ReverseProxy.LogzIo;
using Mentalist.ReverseProxy.Settings;

namespace Mentalist.ReverseProxy.Routing.Middleware;

public class AdvertiseLbMiddleware(RequestDelegate next, IServiceDetailsProvider service)
{
    private static readonly string AssemblyVersion = Assembly.GetAssembly(typeof(AdvertiseLbMiddleware))?.GetName().Version?.ToString() ?? "0.0.0.0";

    private ServiceInformation? _serviceInformation;

    public async Task Invoke(HttpContext context)
    {
        if (!context.Response.HasStarted)
        {
            _serviceInformation ??= service.GetInformation();

            var serviceHost = _serviceInformation.Advertised.Host;
            if (_serviceInformation.Advertised.Port != 80 && _serviceInformation.Advertised.Port != 443)
            {
                serviceHost += $":{_serviceInformation.Advertised.Port}";
            }

            context.Response.Headers["lb-host"] = serviceHost;
            context.Response.Headers["lb-ver"] = AssemblyVersion;

            var activity = Activity.Current;
            if (activity != null)
            {
                var traceId = activity.GetTraceId();
                if (!string.IsNullOrWhiteSpace(traceId))
                {
                    context.Response.Headers["lb-trace-id"] = traceId;
                }
            }
        }

        await next(context).ConfigureAwait(false);
    }
}