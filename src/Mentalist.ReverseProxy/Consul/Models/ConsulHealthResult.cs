namespace Mentalist.ReverseProxy.Consul.Models;

public class ConsulHealthResult
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public ConsulService Service { get; set; } = null!;
    public ConsulServiceCheck[]? Checks { get; set; }
}