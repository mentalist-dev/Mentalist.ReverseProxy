﻿using System.Net;
using System.Text;
using System.Text.Json;
using Mentalist.ReverseProxy.Settings;
using Yarp.ReverseProxy.Configuration;

namespace Mentalist.ReverseProxy.Status;

public class RoutingStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly IServiceDetailsProvider _service;

    private ServiceInformation? _serviceInformation;

    public RoutingStatusMiddleware(RequestDelegate next, IProxyConfigProvider proxyConfigProvider, IServiceDetailsProvider service)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _proxyConfigProvider = proxyConfigProvider;
        _service = service;
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

        var proxyConfig = _proxyConfigProvider.GetConfig();
        _serviceInformation ??= _service.GetInformation();

        var data = new
        {
            Timestamp = DateTime.UtcNow,
            Server = new 
            {
                _serviceInformation.Physical,
                _serviceInformation.Advertised
            },
            Request = new
            {
                context.Request.Host.Host,
                context.Request.Host.Port,
                context.Request.Scheme,
                context.Request.ContentType,
                context.Request.Method,
                context.Request.PathBase,
                context.Request.IsHttps,
                context.Request.Headers
            },
            Routing = new
            {
                proxyConfig.Routes,
                proxyConfig.Clusters
            }
        };

        var serializedData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        return response.Body.WriteAsync(serializedData, context.RequestAborted);
    }
}