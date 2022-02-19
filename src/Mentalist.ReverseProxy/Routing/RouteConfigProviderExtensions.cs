using System.Net;
using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Settings;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Routing;

public static class RouteConfigProviderExtensions
{
    public static IReverseProxyBuilder LoadFromMemory(this IReverseProxyBuilder builder, Dictionary<string, ProxyRoute> proxy)
    {
        builder.Services.AddSingleton<IProxyConfigProvider>(new InMemoryConfigProvider(proxy));
        return builder;
    }

    public static IReverseProxyBuilder LoadFromConsul(this IReverseProxyBuilder builder, ConsulConfiguration configuration)
    {
        builder.Services.AddSingleton(configuration);
        builder.Services.AddHttpClient<IConsulMonitor, ConsulMonitor>();
        builder.Services.AddHostedService<ConsulMonitorHost>();
        builder.Services.AddSingleton<IConsulServiceRegistry, ConsulConfigProvider>();
        builder.Services.AddSingleton<IProxyConfigProvider>(p => p.GetRequiredService<IConsulServiceRegistry>());
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