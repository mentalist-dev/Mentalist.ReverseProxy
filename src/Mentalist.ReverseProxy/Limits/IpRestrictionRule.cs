namespace Mentalist.ReverseProxy.Limits;

public class IpRestrictionRule
{
    public string? Description { get; set; }
    public string[] SourceIp { get; set; } = Array.Empty<string>();
    public string[]? Host { get; set; }
    public string[]? Path { get; set; }
}