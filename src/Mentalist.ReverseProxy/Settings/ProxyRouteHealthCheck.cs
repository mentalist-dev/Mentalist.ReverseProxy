namespace Mentalist.ReverseProxy.Settings;

public class ProxyRouteHealthCheck
{
    public bool Enabled { get; set; }
    public string? Path { get; set; }
}