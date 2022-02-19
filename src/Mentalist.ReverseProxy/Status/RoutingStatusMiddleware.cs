using System.Net;
using System.Text;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Status;

public class RoutingStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyConfigProvider _proxyConfigProvider;

    public RoutingStatusMiddleware(RequestDelegate next, IProxyConfigProvider proxyConfigProvider)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _proxyConfigProvider = proxyConfigProvider;
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

        response.StatusCode = (int)HttpStatusCode.OK;

        var data = new
        {
            Timestamp = DateTime.UtcNow,
            Config = _proxyConfigProvider.GetConfig()
        };

        var serializedData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return response.Body.WriteAsync(serializedData, context.RequestAborted);
    }
}