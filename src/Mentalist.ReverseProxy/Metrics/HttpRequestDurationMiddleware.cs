using System.Diagnostics;
using Prometheus;

namespace Mentalist.ReverseProxy.Metrics;

public class HttpRequestDurationMiddleware
{
    private static readonly Histogram HttpRequests = Prometheus.Metrics.CreateHistogram(
        "http_request_duration_seconds",
        "HTTP Request Duration",
        new HistogramConfiguration
        {
            LabelNames = new[] {"code", "method", "action"},
            Buckets = new [] {0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.75, 1, 1.5, 2, 2.5, 5, 7.5, 10, 20, 30, 40, 50, 60}
        });

    private static readonly Gauge HttpRequestsInProgress = Prometheus.Metrics.CreateGauge(
        "http_request_in_progress",
        "HTTP Requests In Progress",
        new GaugeConfiguration
        {
            LabelNames = new[] {"method", "action"}
        });

    private static readonly Counter HttpRequestCounter = Prometheus.Metrics.CreateCounter(
        "http_requests_received_total",
        "HTTP Request Counter",
        new CounterConfiguration
        {
            LabelNames = new[] {"method", "action"}
        });

    private readonly RequestDelegate _next;

    public HttpRequestDurationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var timer = Stopwatch.StartNew();

        var method = context.Request.Method;
        var pathBase = context.Request.PathBase.ToString();

        var path = context.Request.Path.ToString()
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var action = pathBase;
        var count = 0;
        foreach (var p in path)
        {
            count += 1;

            var node = p;

            if (!string.IsNullOrWhiteSpace(node))
            {
                // do not include Guid's to metrics
                if (Guid.TryParse(node, out _))
                    node = "{id}";

                action += $"/{node}";
            }

            if (count == 2)
                break;
        }

        HttpRequestCounter
            .Labels(method, action)
            .Inc();

        HttpRequestsInProgress
            .Labels(method, action)
            .Inc();

        try
        {
            await _next(context);
        }
        finally
        {
            var code = context.Response.StatusCode;
            var elapsedSeconds = timer.Elapsed.TotalSeconds;

            HttpRequests
                .Labels(code.ToString(), method, action)
                .Observe(elapsedSeconds);

            HttpRequestsInProgress
                .Labels(method, action)
                .Dec();
        }
    }
}