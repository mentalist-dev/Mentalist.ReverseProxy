// ReSharper disable once CheckNamespace
namespace Mentalist.ReverseProxy;

public class RequestInformation
{
    public bool Enabled { get; set; }
    public bool HashedCookies { get; set; }
    public string? HashedCookiesFilter { get; set; }
    public string[]? Headers { get; set; }
    public JwtOptions? Jwt { get; set; }
}

public class JwtOptions
{
    public string? Header { get; set; }
    public bool ParseEnabled { get; set; }
    public string[]? ParseClaims { get; set; }
}