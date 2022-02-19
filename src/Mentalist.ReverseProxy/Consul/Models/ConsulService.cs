namespace Mentalist.ReverseProxy.Consul.Models;

public class ConsulService
{
    public string Id { get; set; } = string.Empty;
    public string? Service { get; set; }
    public string[]? Tags { get; set; }
    public string? Address { get; set; }
    public int? Port { get; set; }
}