using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Metrics;
using Mentalist.ReverseProxy.Routing;
using Mentalist.ReverseProxy.Settings;
using Mentalist.ReverseProxy.Status;
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
    .AddJsonFile("serilog.json", optional: false)
    .AddJsonFile("services.json", optional: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var proxySettings = new Dictionary<string, ProxyRoute>();
builder.Configuration.GetSection("ReverseProxyRoutes").Bind(proxySettings);
builder.Services.AddSingleton(_ => proxySettings);

var consul = new ConsulConfiguration();
builder.Configuration.GetSection("Consul").Bind(consul);
builder.Services.AddSingleton(_ => consul);

builder.Services
    .AddReverseProxy()
    .LoadFromConsul(consul);
    //.LoadFromMemory(proxySettings);

var app = builder.Build();

app.AddServiceUnavailableEndpoint();
app.Map("/status", b => b.UseMiddleware<StatusMiddleware>());
app.Map("/routing-status", b => b.UseMiddleware<RoutingStatusMiddleware>());

app.UseRouting();
app.UseHttpMetrics();
app.UseEndpoints(endpoints =>
{
    endpoints.MapMetrics();
});

app.MapReverseProxy();

app.Run();
