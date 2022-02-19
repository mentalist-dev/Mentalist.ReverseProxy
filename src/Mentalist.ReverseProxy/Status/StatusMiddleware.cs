using System.Net;
using System.Text;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Status;

public class StatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly byte[] _response;

    public StatusMiddleware(RequestDelegate next, IProxyConfigProvider proxyConfigProvider)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _proxyConfigProvider = proxyConfigProvider;

        var data = new
        {
            Timestamp = DateTime.UtcNow
        };

        _response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions
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
            response.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
            return ValueTask.CompletedTask;
        }

        response.StatusCode = (int)HttpStatusCode.OK;
        return response.Body.WriteAsync(_response, context.RequestAborted);
    }
}