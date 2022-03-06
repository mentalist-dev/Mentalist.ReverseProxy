using System.Diagnostics;
using System.Net;

namespace Mentalist.ReverseProxy.Routing.Middleware;

public class RequestInformationMiddleware
{
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
            int responseStatusCode = context.Response.StatusCode;
            var statusCode = ((HttpStatusCode)responseStatusCode).ToString();

            var logLevel = LogLevel.Information;

            if (responseStatusCode >= 500)
            {
                logLevel = LogLevel.Error;
            }
            else if (responseStatusCode >= 400)
            {
                logLevel = LogLevel.Warning;
            }

            var elapsedMilliseconds = timer.ElapsedMilliseconds;
            if (elapsedMilliseconds >= 1000 || logLevel == LogLevel.Error || logLevel == LogLevel.Warning)
            {
                _logger.Log(logLevel, lastException,
                    "Request finished in {ElapsedMilliseconds}ms {StatusCode}/{StatusCodeName} {ContentType}",
                    elapsedMilliseconds, responseStatusCode, statusCode, context.Response.ContentType);
            }
        }
    }
}

public class RequestInformation { }
