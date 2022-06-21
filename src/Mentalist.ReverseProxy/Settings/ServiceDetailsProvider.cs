using System.Net;
using System.Net.Sockets;
using Mentalist.ReverseProxy.Consul;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Mentalist.ReverseProxy.Settings;

public interface IServiceDetailsProvider
{
    ServiceInformation GetInformation();
}

public class ServiceInformation
{
    public ServiceInformation(ServiceAddress physical, ServiceAddress advertised)
    {
        Physical = physical;
        Advertised = advertised;
    }

    public ServiceAddress Physical { get; }
    public ServiceAddress Advertised { get; }
}

public class ServiceAddress
{
    public ServiceAddress(string host, int port)
    {
        Host = host;
        Port = port;
    }

    public string Host { get; }
    public int Port { get; }
}

public class ServiceDetailsProvider: IServiceDetailsProvider
{
    private readonly IServer _server;
    private readonly ConsulConfiguration _configuration;

    public ServiceDetailsProvider(IServer server, ConsulConfiguration configuration)
    {
        _server = server;
        _configuration = configuration;
    }

    public ServiceInformation GetInformation()
    {
        var serverHost = GetLocalIpAddress();
        var serverPort = 80;

        if (string.IsNullOrWhiteSpace(_configuration.PhysicalAddress))
        {
            var addressFeature = _server.Features.Get<IServerAddressesFeature>();
            if (addressFeature?.Addresses.Count > 0)
            {
                foreach (var address in addressFeature.Addresses)
                {
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        var uri = new Uri(address);
                        serverPort = uri.Port;
                        break;
                    }
                }
            }
        }
        else
        {
            var nodes = _configuration.PhysicalAddress.Split(':');
            serverHost = string.Join(':', nodes, 0, nodes.Length - 1);
            if (nodes.Length > 1 && int.TryParse(nodes[^1], out var advertisePort))
            {
                serverPort = advertisePort;
            }
        }

        var advertisedHost = serverHost;
        var advertisedPort = serverPort;
        if (!string.IsNullOrWhiteSpace(_configuration.AdvertiseAddress))
        {
            var nodes = _configuration.AdvertiseAddress.Split(':');
            advertisedHost = string.Join(':', nodes, 0, nodes.Length - 1);
            if (nodes.Length > 1 && int.TryParse(nodes[^1], out var advertisePort))
            {
                advertisedPort = advertisePort;
            }
        }

        var physical = new ServiceAddress(serverHost, serverPort);
        var advertised = new ServiceAddress(advertisedHost, advertisedPort);
        return new ServiceInformation(physical, advertised);
    }

    private static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }

        return string.Empty;
    }
}