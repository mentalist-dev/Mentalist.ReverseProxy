using System.Net;
using Mentalist.ReverseProxy.Limits;
using Xunit;

namespace Mentalist.ReverseProxy.Tests;

public class RuleValidationShould
{
    [Theory]
    [InlineData("app.example.com")]
    [InlineData("dev.example.com")]
    [InlineData("qa.example.com")]
    public void ValidateHosts(string requestHost)
    {
        var patterns = new[]
        {
            "app.example.com",
            "dev.example.com",
            "qa.example.com",
        };

        var isAllowed = RestrictionConfiguration.IsHostMatching(requestHost, patterns);
        Assert.True(isAllowed);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("hello.example.com")]
    [InlineData("www.google.com")]
    public void InvalidateHosts(string requestHost)
    {
        var patterns = new[]
        {
            "app.example.com",
            "dev.example.com",
            "qa.example.com",
        };

        var isAllowed = RestrictionConfiguration.IsHostMatching(requestHost, patterns);
        Assert.False(isAllowed);
    }

    [Theory]
    [InlineData("app.example.com")]
    [InlineData("dev.example.com")]
    [InlineData("qa.example.com")]
    public void ValidateHostPattern(string requestHost)
    {
        var patterns = new[]
        {
            ".example.com"
        };

        var isAllowed = RestrictionConfiguration.IsHostMatching(requestHost, patterns);
        Assert.True(isAllowed);
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/status")]
    [InlineData("/api/v1/health")]
    public void ValidatePath(string requestPath)
    {
        var patterns = new[]
        {
            "/*/health",
            "/admin",
            "/status",
        };

        var isAllowed = RestrictionConfiguration.IsPathMatching(requestPath, patterns);
        Assert.True(isAllowed);
    }

    [Theory]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.2.1")]
    [InlineData("192.168.2.100")]
    [InlineData("192.168.240.100")]
    [InlineData("10.10.11.12")]
    public void ValidateIp(string requestIp)
    {
        var patterns = new[]
        {
            "192.168.0.0/16",
            "10.10.11.12/32"
        };

        var isAllowed = RestrictionConfiguration.IsIpMatching(IPAddress.Parse(requestIp), patterns);
        Assert.True(isAllowed);
    }
}