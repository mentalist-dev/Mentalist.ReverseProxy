using System.Net;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;

namespace Mentalist.ReverseProxy.Routing.Middleware;

public class EnforceHttpsMiddleware(
    RequestDelegate next,
    RoutingConfiguration routing,
    ILogger<EnforceHttpsMiddleware> logger)
{
    private readonly StringValues _hstsHeader = new("max-age=31536001; includeSubDomains; preload");

    public Task Invoke(HttpContext context)
    {
        if (!routing.AssumeHttps && routing.HttpPort > 0 && context.Connection.LocalPort == routing.HttpPort)
        {
            context.Request.Scheme = "http";

            var request = context.Request;
            var requestPath = request.Path.ToString().ToLower();

            if (!requestPath.EndsWith("/status") && !requestPath.EndsWith("/metrics") && !requestPath.EndsWith("/routing-status"))
            {
                var host = request.Host;

                var port = routing.HttpsPort;
                if (port <= 0)
                    port = 443;

                var scheme = routing.HttpsScheme;
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

                logger.LogWarning("Redirecting {Url} to HTTPS: {RedirectUrl}", context.Request.GetDisplayUrl(), redirectUrl);

                return Task.CompletedTask;
            }

            return next(context);
        }

        context.Request.Scheme = "https";

        if (routing.EnableHsts)
        {
            context.Response.Headers.StrictTransportSecurity = _hstsHeader;
        }

        return next(context);
    }
}