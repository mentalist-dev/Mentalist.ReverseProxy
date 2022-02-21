using System.Net;
using System.Text.RegularExpressions;

namespace Mentalist.ReverseProxy.Limits;

public class RestrictionConfiguration
{
    private static readonly IpRestrictionValidationResult IsAllowedResult = new(true);

    public int? RequestSizeLimitMb { get; set; }
    public bool IpRestrictionsEnabled { get; set; }
    public Dictionary<string, IpRestrictionRule>? IpRestrictionRules { get; set; }

    public IpRestrictionValidationResult IpIsAllowed(HttpContext context)
    {
        var rules = IpRestrictionRules;

        if (rules == null || rules.Count == 0)
            return IsAllowedResult;

        foreach (var rule in rules)
        {
            var ruleName = rule.Key;

            bool ipMatch = false;
            bool? hostMatch = null;
            bool? pathMatch = null;

            var ips = rule.Value.SourceIp;
            if (ips.Length == 0)
            {
                // if IP is not specified - nothing can be restricted
                continue;
            }

            if (ips.Length > 0)
            {
                if (context.Connection.RemoteIpAddress == null)
                    return new IpRestrictionValidationResult(false, ruleName, rule.Value);

                var incomingIp = context.Connection.RemoteIpAddress;
                if (context.Connection.RemoteIpAddress.IsIPv4MappedToIPv6)
                {
                    incomingIp = context.Connection.RemoteIpAddress.MapToIPv4();
                }

                ipMatch = IsIpMatching(incomingIp, ips);
            }

            var hosts = rule.Value.Host;
            if (hosts?.Length > 0)
            {
                var requestHost = context.Request.Host.Host;
                hostMatch = IsHostMatching(requestHost, hosts);
            }

            var paths = rule.Value.Path;
            if (paths?.Length > 0)
            {
                var requestPath = context.Request.Path.ToString();

                pathMatch = IsPathMatching(requestPath, paths);
            }

            if ((hostMatch == true || pathMatch == true) && ipMatch != true)
            {
                return new IpRestrictionValidationResult(false, ruleName, rule.Value);
            }
        }

        return IsAllowedResult;
    }

    public static bool IsHostMatching(string requestHost, string[] hosts)
    {
        var isAllowed = false;
        foreach (var host in hosts)
        {
            if (Regex.IsMatch(requestHost, host, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                isAllowed = true;
                break;
            }
        }

        return isAllowed;
    }

    public static bool IsPathMatching(string requestPath, string[] paths)
    {
        var isAllowed = false;
        foreach (var path in paths)
        {
            if (Regex.IsMatch(requestPath, path, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                isAllowed = true;
                break;
            }
        }

        return isAllowed;
    }

    public static bool IsIpMatching(IPAddress incomingIp, string[] ips)
    {
        var isAllowed = false;
        foreach (var subnet in ips)
        {
            var network = IPNetwork.Parse(subnet);
            if (network.Contains(incomingIp))
            {
                isAllowed = true;
                break;
            }
        }

        return isAllowed;
    }
}