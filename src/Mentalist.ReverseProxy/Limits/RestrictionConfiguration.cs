using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Text.RegularExpressions;

namespace Mentalist.ReverseProxy.Limits;

public class RestrictionConfiguration
{
    private static readonly IpRestrictionValidationResult IsAllowedResult = IpRestrictionValidationResult.IsAllowedResult();

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

            bool? hostMatch = null;
            bool? pathMatch = null;

            var ips = rule.Value.SourceIp;
            if (ips.Length == 0)
            {
                // if IP is not specified - nothing can be restricted
                continue;
            }

            var incomingIp = GetCallerIp(context);
            if (incomingIp == null)
                return IpRestrictionValidationResult.IsNotAllowedResult(ruleName, rule.Value, "null");

            var ipMatch = IsIpMatching(incomingIp, ips);

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

            if ((hostMatch == true || pathMatch == true) && !ipMatch)
            {
                return IpRestrictionValidationResult.IsNotAllowedResult(ruleName, rule.Value, incomingIp.ToString());
            }
        }

        return IsAllowedResult;
    }

    public static IPAddress? GetCallerIp(HttpContext context)
    {
        IPAddress? parsed;
        var headers = context.Request.Headers;

        if (headers.TryGetValue("X-Forwarded-For", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            var addressList = value.ToString().Split(',');
            if (addressList.Length > 0)
            {
                foreach (var address in addressList)
                {
                    var ip = address;
                    if (ip.StartsWith("::ffff:"))
                        ip = ip.Substring("::ffff:".Length);

                    if (IPAddress.TryParse(ip, out parsed))
                        return parsed;
                }
            }
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(ipAddress))
            ipAddress = context.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress?.ToString();

        if (ipAddress?.StartsWith("::ffff:") == true)
            ipAddress = ipAddress.Substring("::ffff:".Length);

        if (!string.IsNullOrWhiteSpace(ipAddress) && IPAddress.TryParse(ipAddress, out parsed))
            return parsed;

        return null;
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