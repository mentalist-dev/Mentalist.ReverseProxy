namespace Mentalist.ReverseProxy.Consul;

public class ConsulConfiguration
{
    /*
     * Sanity check index is greater than zero.
     *  After the initial request (or a reset as above) the X-Consul-Index returned should always be greater than zero.
     *  It is a bug in Consul if it is not, however this has happened a few times and can still be triggered on some older Consul versions.
     *  It's especially bad because it causes blocking clients that are not aware to enter a busy loop, using excessive client CPU and causing high load on servers.
     *  It is always safe to use an index of 1 to wait for updates when the data being requested doesn't exist yet, so clients should sanity check that their index
     *  is at least 1 after each blocking response is handled to be sure they actually block on the next request.
     *
     *  Source: https://www.consul.io/api-docs/features/blocking
     */

    public const long BlockingIndex = 1;
    public const long ResetIndex = 0;
    public const string DefaultTag = "urlprefix-";
    public const string Passing = "passing";

    public string Endpoint { get; set; } = string.Empty;
    public string Tag { get; set; } = DefaultTag;
    public string Advertise { get; set; } = DefaultTag;
}