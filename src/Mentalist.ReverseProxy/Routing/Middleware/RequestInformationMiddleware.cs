using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Mentalist.ReverseProxy.Settings;
using Mentalist.ReverseProxy.Tools;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly App _settings;
    private readonly RequestInformation _requestInformation;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public RequestInformationMiddleware(RequestDelegate next, App settings, RequestInformation requestInformation, IMemoryCache cache, ILogger<RequestInformation> logger)
    {
        _next = next;
        _settings = settings;
        _requestInformation = requestInformation;
        _cache = cache;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var timer = Stopwatch.StartNew();

        var method = context.Request.Method;
        var action = context.Request.CreatePathTemplate();

        using var loggerScope = BuildLoggerScope(context, method);

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
            if (context.RequestAborted.IsCancellationRequested || 
                lastException?.Message.StartsWith("The client has disconnected") == true ||
                lastException?.InnerException is COMException com && com.Message.StartsWith("The specified network name is no longer available."))
            {
                HttpCancelledRequestCounter.Labels(method, action).Inc();

                logLevel = LogLevel.Warning;
                message = "Request cancelled after";
            }

            var elapsedMilliseconds = timer.ElapsedMilliseconds;
            if (elapsedMilliseconds >= _settings.LogWhenRequestIsLongerThanMilliseconds || logLevel == LogLevel.Error || logLevel == LogLevel.Warning)
            {
                _logger.Log(logLevel, lastException,
                    message + " {ElapsedMilliseconds}ms {StatusCode}/{StatusCodeName} {ContentType}",
                    elapsedMilliseconds, statusCode, statusCodeName, context.Response.ContentType);
            }
        }
    }

    private IDisposable BuildLoggerScope(HttpContext context, string method)
    {
        var state = new List<KeyValuePair<string, object>>
        {
            new("requestMethod", method)
        };

        if (_requestInformation.Enabled)
        {
            if (_requestInformation.Headers?.Length > 0)
            {
                foreach (var headerName in _requestInformation.Headers)
                {
                    var header = context.Request.Headers[headerName];
                    var value = header.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        state.Add(new KeyValuePair<string, object>($"headers.{headerName}", value));
                    }
                }
            }

            ParseJwtToken(context.Request, state);

            if (_requestInformation.HashedCookies)
            {
                var filter = _requestInformation.HashedCookiesFilter;

                foreach (var cookie in context.Request.Cookies)
                {
                    var name = cookie.Key.ToLower();
                    if (!string.IsNullOrWhiteSpace(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var value = cookie.Value.ToSHA1();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        state.Add(new KeyValuePair<string, object>($"cookie.{name}", value));
                    }
                }
            }
        }

        return _logger.BeginScope(state);
    }

    private void ParseJwtToken(HttpRequest request, List<KeyValuePair<string, object>> state)
    {
        var jwtHeader = _requestInformation.Jwt?.Header;
        var jwtParsingEnabled = _requestInformation.Jwt?.ParseEnabled ?? false;
        var jwtClaimNames = _requestInformation.Jwt?.ParseClaims;

        if (string.IsNullOrWhiteSpace(jwtHeader))
            return;

        var headerName = jwtHeader;
        var header = request.Headers[headerName];
        var authorizationHeaderValue = header.ToString();

        if (!string.IsNullOrWhiteSpace(authorizationHeaderValue))
        {
            var cacheKey = authorizationHeaderValue.ToSHA1();
            if (!_cache.TryGetValue<TokenContainer>(cacheKey, out var container))
            {
                var length = authorizationHeaderValue.IndexOf(' ');
                if (length > 0)
                {
                    // Basic, Bearer, Digest, HOBA, Mutual, AWS4-HMAC-SHA256
                    if (length > 16)
                        length = 16;

                    var tokenType = authorizationHeaderValue.Substring(0, length).Trim();
                    container = new TokenContainer(tokenType);

                    if (jwtParsingEnabled && jwtClaimNames?.Length > 0)
                    {
                        var token = authorizationHeaderValue.Substring(length).Trim();
                        var parsed = TokenParser.Parse(token);
                        if (parsed != null)
                        {
                            foreach (var claimName in jwtClaimNames)
                            {
                                if (parsed.TryGetValue(claimName, out var claim))
                                {
                                    container.Claims.Add(new KeyValuePair<string, object>(claimName, claim));
                                }
                            }
                        }
                    }
                }
                else
                {
                    container = new TokenContainer(string.Empty);
                }

                _cache.Set(cacheKey, container, TimeSpan.FromHours(2));
            }

            container.FillState(state);
        }
    }

    public class TokenContainer
    {
        public TokenContainer(string type)
        {
            Type = type;
        }

        public string Type { get; }
        public List<KeyValuePair<string, object>> Claims { get; } = new();

        public void FillState(List<KeyValuePair<string, object>> state)
        {
            if (!string.IsNullOrWhiteSpace(Type))
            {
                state.Add(new KeyValuePair<string, object>("tokenType", Type));
            }
            state.AddRange(Claims);
        }
    }
}