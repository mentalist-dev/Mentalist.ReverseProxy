using Mentalist.ReverseProxy.Consul;
using Mentalist.ReverseProxy.Limits;
using Mentalist.ReverseProxy.LogzIo;
using Mentalist.ReverseProxy.Metrics;
using Mentalist.ReverseProxy.Routing;
using Mentalist.ReverseProxy.Routing.Middleware;
using Mentalist.ReverseProxy.Settings;
using Mentalist.ReverseProxy.Status;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.Logz.Io;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.overrides.json", optional: true)
    .AddJsonFile("serilog.json", optional: false)
    .AddJsonFile("services.json", optional: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var consul = builder.Configuration.GetSection("Consul").Get<ConsulConfiguration>();
builder.Services.AddSingleton(_ => consul);

var logzIo = builder.Configuration.GetSection("LogzIo").Get<LogzIoConfiguration>();
builder.Services.AddSingleton(_ => logzIo);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Prometheus();

    configuration.Enrich.With(new ServiceNameEnricher(consul.ServiceName));

    if (!string.IsNullOrWhiteSpace(logzIo.Url))
    {
        var textFormatterOptions = new LogzioTextFormatterOptions
        {
            BoostProperties = logzIo.BoostProperties,
            IncludeMessageTemplate = logzIo.IncludeMessageTemplate,
            LowercaseLevel = logzIo.LowercaseLevel,
            FieldNaming = LogzIoTextFormatterFieldNaming.CamelCase
        };

        var bufferPathFormat = logzIo.BufferPathFormat;
        if (string.IsNullOrWhiteSpace(bufferPathFormat))
            bufferPathFormat = "buffer-{{Hour}}.json";

        configuration.WriteTo.LogzIoDurableHttp(
            logzIo.Url,
            bufferPathFormat,
            logzioTextFormatterOptions: textFormatterOptions
        );
    }

    SelfLog.Enable(Console.Out);
});

var proxySettings = new Dictionary<string, ProxyRoute>();
builder.Configuration.GetSection("ReverseProxyRoutes").Bind(proxySettings);

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
app.UseMiddleware<RequestInformationMiddleware>();

if (requestRestrictions.IpRestrictionsEnabled && requestRestrictions.IpRestrictionRules?.Count > 0)
{
    app.UseMiddleware<RestrictionValidationMiddleware>();
}

if (routing.ForceHttps)
{
    app.UseMiddleware<EnforceHttpsMiddleware>();
}

// app.UseHttpLogging();

app.AddServiceUnavailableEndpoint();
app.Map("/status", b => b.UseMiddleware<StatusMiddleware>());
app.Map("/routing-status", b => b.UseMiddleware<RoutingStatusMiddleware>());

app.UseRouting();

app.UseMiddleware<HttpRequestDurationMiddleware>();
// prometheus metrics produces too many metrics
// app.UseHttpMetrics();

app.UseEndpoints(endpoints =>
{
    var metricsPath = metrics.Path;
    if (string.IsNullOrWhiteSpace(metricsPath))
        metricsPath = "/metrics";
    endpoints.MapMetrics(metricsPath);
});

app.MapReverseProxy();

app.Run();
