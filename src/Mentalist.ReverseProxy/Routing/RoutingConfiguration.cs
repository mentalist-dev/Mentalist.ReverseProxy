namespace Mentalist.ReverseProxy.Routing;

public class RoutingConfiguration
{
    public bool ForceHttps { get; set; } = false;
    public bool EnableHsts { get; set; } = true;
    public bool AssumeHttps { get; set; } = false;

    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;
    public string HttpsScheme { get; set; } = "https";
    public string XFrameOptions { get; set; } = "SAMEORIGIN";
}