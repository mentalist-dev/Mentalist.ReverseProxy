﻿using System.Diagnostics;
using System.Net;
using Prometheus;

namespace Mentalist.ReverseProxy.Routing.Middleware;

public class RequestInformationMiddleware
{
    private static readonly Counter HttpCancelledRequestCounter = Prometheus.Metrics.CreateCounter(
        "http_requests_cancelled_total",
        "HTTP Cancelled Request Counter",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "action" }
        });

    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public RequestInformationMiddleware(RequestDelegate next, ILogger<RequestInformation> logger)
    {
        _next = next;
        _logger = logger;
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
            if (!string.IsNullOrWhiteSpace(p))
                action += $"/{p}";

            if (count == 2)
                break;
        }

        Exception? lastException = null;
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lastException = ex;
            throw;
        }
        finally
        {
            int statusCode = context.Response.StatusCode;
            var statusCodeName = ((HttpStatusCode)statusCode).ToString();

            var logLevel = LogLevel.Information;

            if (statusCode >= 500)
            {
                logLevel = LogLevel.Error;
            }
            else if (statusCode >= 400)
            {
                logLevel = LogLevel.Warning;
            }

            var message = "Request finished in";
            if (context.RequestAborted.IsCancellationRequested)
            {
                HttpCancelledRequestCounter.Labels(method, action).Inc();

                logLevel = LogLevel.Warning;
                message = "Request cancelled after";
            }

            var elapsedMilliseconds = timer.ElapsedMilliseconds;
            if (elapsedMilliseconds >= 1000 || logLevel == LogLevel.Error || logLevel == LogLevel.Warning)
            {
                _logger.Log(logLevel, lastException,
                    message + " {ElapsedMilliseconds}ms {StatusCode}/{StatusCodeName} {ContentType}",
                    elapsedMilliseconds, statusCode, statusCodeName, context.Response.ContentType);
            }
        }
    }
}

public class RequestInformation { }