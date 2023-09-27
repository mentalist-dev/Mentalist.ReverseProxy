using System.Net;
using System.Text;
using System.Text.Json;

namespace Mentalist.ReverseProxy.Limits;

public class RestrictionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RestrictionConfiguration _restrictions;
    private readonly ILogger<RestrictionValidationMiddleware> _logger;

    public RestrictionValidationMiddleware(RequestDelegate next, RestrictionConfiguration restrictions, ILogger<RestrictionValidationMiddleware> logger)
    {
        _next = next;
        _restrictions = restrictions;
        _logger = logger;
    }

    public Task Invoke(HttpContext context)
    {
        var validationResult = _restrictions.IpIsAllowed(context);
        if (validationResult.IsAllowed)
        {
            return _next(context);
        }

        _logger.LogWarning(
            "Request is not allowed because of restrictions. Violated rule name: {ViolatedRuleName}. Violated rule = {@ViolatedRule}",
            validationResult.ViolatedRuleName, validationResult.ViolatedRule
        );

        var data = new
        {
            Error = $"Request is not allowed [{validationResult.ViolatedRuleName}: {validationResult.ViolatedRule?.Description}]"
        };

        var serializedData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        context.Response.StatusCode = (int) HttpStatusCode.Forbidden;

        return context.Response.Body
            .WriteAsync(serializedData, context.RequestAborted)
            .AsTask();
    }
}