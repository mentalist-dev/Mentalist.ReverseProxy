using System.Net;
using Microsoft.AspNetCore.Http.Extensions;

namespace Mentalist.ReverseProxy.Routing.Middleware;

public class EnforceHttpsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RoutingConfiguration _routing;
    private readonly ILogger<EnforceHttpsMiddleware> _logger;

    public EnforceHttpsMiddleware(RequestDelegate next, RoutingConfiguration routing, ILogger<EnforceHttpsMiddleware> logger)
    {
        _next = next;
        _routing = routing;
        _logger = logger;
    }

    public Task Invoke(HttpContext context)
    {
        if (_routing.HttpPort > 0 && context.Connection.LocalPort == _routing.HttpPort)
        {
            context.Request.Scheme = "http";

            var request = context.Request;
            var requestPath = request.Path.ToString().ToLower();

            if (!requestPath.EndsWith("/status") && !requestPath.EndsWith("/metrics"))
            {
                var host = request.Host;

                var port = _routing.HttpsPort;
                if (port <= 0)
                    port = 443;

                var scheme = _routing.HttpsScheme;
                if (string.IsNullOrWhiteSpace(scheme))
                    scheme = "https";

                var targetHost = new HostString(host.Host, port);

                var redirectUrl = UriHelper.BuildAbsolute(
                    scheme,
                    targetHost,
                    request.PathBase,
                    request.Path,
                    request.QueryString);

                context.Response.StatusCode = (int) HttpStatusCode.PermanentRedirect;
                context.Response.Headers.Location = redirectUrl;

                _logger.LogWarning("Redirecting to HTTPS: {RedirectUrl}", redirectUrl);

                return Task.CompletedTask;
            }

            return _next(context);
        }

        context.Request.Scheme = "https";
        return _next(context);
    }
}