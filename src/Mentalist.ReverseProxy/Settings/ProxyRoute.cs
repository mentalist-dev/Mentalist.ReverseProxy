namespace Mentalist.ReverseProxy.Settings;

public class ProxyRoute
{
    public string? Path { get; set; }
    public string? Prefix { get; set; }
    public string[]? Endpoints { get; set; }
    public ProxyRouteHealthCheck? HealthCheck { get; set; }
    public bool UseOriginalHost { get; set; } = true;
}
