using System.Net;
using System.Text;
using System.Text.Json;
using Mentalist.ReverseProxy.Limits;
using Mentalist.ReverseProxy.Routing;
using Mentalist.ReverseProxy.Settings;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Status;

public class RoutingStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly IServiceDetailsProvider _service;
    private readonly RestrictionConfiguration _restrictions;
    private readonly StaticRouteConfiguration _staticRoutes;

    private ServiceInformation? _serviceInformation;

    public RoutingStatusMiddleware(RequestDelegate next
        , IProxyConfigProvider proxyConfigProvider
        , IServiceDetailsProvider service
        , RestrictionConfiguration restrictions
        , StaticRouteConfiguration staticRoutes)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _proxyConfigProvider = proxyConfigProvider;
        _service = service;
        _restrictions = restrictions;
        _staticRoutes = staticRoutes;
    }

    public Task Invoke(HttpContext context)
    {
        var writeTask = WriteIsAliveStatus(context);
        return writeTask.IsCompletedSuccessfully
            ? Task.CompletedTask
            : writeTask.AsTask();
    }

    private ValueTask WriteIsAliveStatus(HttpContext context)
    {
        var response = context.Response;

        response.StatusCode = (int)HttpStatusCode.OK;

        var proxyConfig = _proxyConfigProvider.GetConfig();
        _serviceInformation ??= _service.GetInformation();

        var resolvedIpAddress = RestrictionConfiguration.GetCallerIp(context);
        var remoteIpAddress = context.Connection.RemoteIpAddress;

        var data = new
        {
            Timestamp = DateTime.UtcNow,
            Connection = remoteIpAddress == null ? null : new
            {
                AddressFamily = remoteIpAddress.AddressFamily.ToString(),
                remoteIpAddress.IsIPv4MappedToIPv6,
                remoteIpAddress.IsIPv6LinkLocal,
                remoteIpAddress.IsIPv6Multicast,
                remoteIpAddress.IsIPv6SiteLocal,
                remoteIpAddress.IsIPv6Teredo,
                remoteIpAddress.IsIPv6UniqueLocal,
                RemoteIpAddress = remoteIpAddress.ToString()
            },
            ResolvedIpAddress = resolvedIpAddress?.ToString(),
            Server = new 
            {
                _serviceInformation.Physical,
                _serviceInformation.Advertised
            },
            Request = new
            {
                context.Request.Host.Host,
                context.Request.Host.Port,
                context.Request.Scheme,
                context.Request.ContentType,
                context.Request.Method,
                context.Request.PathBase,
                context.Request.IsHttps,
                context.Request.Headers
            },
            Restrictions = _restrictions,
            StaticRouting = _staticRoutes,
            Routing = new
            {
                proxyConfig.Routes,
                proxyConfig.Clusters
            },
        };

        var serializedData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return response.Body.WriteAsync(serializedData, context.RequestAborted);
    }
}