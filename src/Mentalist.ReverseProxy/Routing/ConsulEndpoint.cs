namespace Mentalist.ReverseProxy.Routing;

public class ConsulEndpoint
{
    public string? Address { get; set; }
    public int? Port { get; set; }
    public bool Healthy { get; set; }
}