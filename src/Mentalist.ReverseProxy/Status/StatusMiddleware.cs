using System.Net;
using System.Text;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Status;

public class StatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly byte[] _healthyResponse;
    private readonly byte[] _unhealthyResponse;

    public StatusMiddleware(RequestDelegate next, IProxyConfigProvider proxyConfigProvider)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _proxyConfigProvider = proxyConfigProvider;

        var healthy = new
        {
            Status = "Healthy"
        };

        _healthyResponse = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(healthy, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        var unhealthy = new
        {
            Status = "No routes available"
        };

        _unhealthyResponse = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(unhealthy, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    public Task Invoke(HttpContext context)
    {
        var writeTask = WriteIsAliveStatus(context);
        return writeTask.IsCompletedSuccessfully
            ? Task.CompletedTask
            : writeTask.AsTask();
    }

    private ValueTask WriteIsAliveStatus(HttpContext context)
    {
        var response = context.Response;

        var config = _proxyConfigProvider.GetConfig();
        if (config.Routes.Count == 0)
        {
            response.StatusCode = (int) HttpStatusCode.TooManyRequests; // put service to warning state
            return response.Body.WriteAsync(_unhealthyResponse, context.RequestAborted);
        }

        response.StatusCode = (int)HttpStatusCode.OK;
        return response.Body.WriteAsync(_healthyResponse, context.RequestAborted);
    }
}