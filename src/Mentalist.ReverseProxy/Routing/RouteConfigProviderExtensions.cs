using System.Net;
using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Routing.Providers;
using Mentalist.ReverseProxy.Settings;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Routing;

public static class RouteConfigProviderExtensions
{
    public static IReverseProxyBuilder LoadFromConsul(this IReverseProxyBuilder builder, ConsulConfiguration consul)
    {
        if (consul.Enabled)
        {
            builder.Services.AddSingleton(consul);
            builder.Services.AddHttpClient<IConsulMonitor, ConsulMonitor>();
            builder.Services.AddSingleton<IConsulServiceRegistry, ConsulConfigProvider>();
            builder.Services.AddSingleton<IProxyConfigProvider>(p => p.GetRequiredService<IConsulServiceRegistry>());
            builder.Services.AddHostedService<ConsulMonitorHost>();
        }

        return builder;
    }

    public static IReverseProxyBuilder LoadFromConfig(this IReverseProxyBuilder builder, Dictionary<string, ProxyRoute> routes)
    {
        builder.Services.AddSingleton(new StaticRouteConfiguration(routes));
        builder.Services.AddSingleton<IProxyConfigProvider, StaticRouteConfigProvider>();
        return builder;
    }

    public static IApplicationBuilder AddServiceUnavailableEndpoint(this IApplicationBuilder app)
    {
        return app.Map("/unavailable", builder => builder.Run(async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsync("Service Unavailable.");
        }));
    }
}