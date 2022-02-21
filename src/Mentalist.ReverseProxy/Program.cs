using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Limits;
using Mentalist.ReverseProxy.Metrics;
using Mentalist.ReverseProxy.Routing;
using Mentalist.ReverseProxy.Routing.Middleware;
using Mentalist.ReverseProxy.Settings;
using Mentalist.ReverseProxy.Status;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Prometheus();
});

builder.Configuration
    .AddJsonFile("appsettings.overrides.json", optional: true)
    .AddJsonFile("serilog.json", optional: false)
    .AddJsonFile("services.json", optional: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var proxySettings = new Dictionary<string, ProxyRoute>();
builder.Configuration.GetSection("ReverseProxyRoutes").Bind(proxySettings);

var consul = new ConsulConfiguration();
builder.Configuration.GetSection("Consul").Bind(consul);
builder.Services.AddSingleton(_ => consul);

var metrics = new MetricsConfiguration();
builder.Configuration.GetSection("Metrics").Bind(metrics);
builder.Services.AddSingleton(_ => metrics);

var routing = new RoutingConfiguration();
builder.Configuration.GetSection("Routing").Bind(routing);
builder.Services.AddSingleton(_ => routing);

var requestRestrictions = new RestrictionConfiguration();
builder.Configuration.GetSection("Restrictions").Bind(requestRestrictions);
builder.Services.AddSingleton(_ => requestRestrictions);

builder.Services
    .AddReverseProxy()
    .LoadFromConsul(consul);
    //.LoadFromMemory(proxySettings);

builder.Services.AddSingleton<IServiceDetailsProvider, ServiceDetailsProvider>();

if (requestRestrictions.RequestSizeLimitMb > 0)
{
    builder.Services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = requestRestrictions.RequestSizeLimitMb.Value * 1024 * 1024; // 500 MB
    });
}

var app = builder.Build();

app.UseMiddleware<AdvertiseLbMiddleware>();

if (requestRestrictions.IpRestrictionsEnabled && requestRestrictions.IpRestrictionRules?.Count > 0)
{
    app.UseMiddleware<RestrictionValidationMiddleware>();
}

if (routing.ForceHttps)
{
    app.UseMiddleware<EnforceHttpsMiddleware>();
}

app.AddServiceUnavailableEndpoint();
app.Map("/status", b => b.UseMiddleware<StatusMiddleware>());
app.Map("/routing-status", b => b.UseMiddleware<RoutingStatusMiddleware>());

app.UseRouting();
app.UseHttpMetrics();
app.UseEndpoints(endpoints =>
{
    var metricsPath = metrics.Path;
    if (string.IsNullOrWhiteSpace(metricsPath))
        metricsPath = "/metrics";
    endpoints.MapMetrics(metricsPath);
});

app.MapReverseProxy();

app.Run();
