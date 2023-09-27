namespace Mentalist.ReverseProxy.Limits;

public class IpRestrictionValidationResult
{
    private IpRestrictionValidationResult(bool isAllowed, string? violatedRuleName = null, IpRestrictionRule? violatedRule = null, string? validatedIp = null)
    {
        IsAllowed = isAllowed;
        ViolatedRuleName = violatedRuleName;
        ViolatedRule = violatedRule;
        ValidatedIp = validatedIp;
    }

    public static IpRestrictionValidationResult IsAllowedResult() => new(true);

    public static IpRestrictionValidationResult IsNotAllowedResult(string? violatedRuleName = null, IpRestrictionRule? violatedRule = null, string? validatedIp = null)
    {
        return new(false, violatedRuleName, violatedRule, validatedIp);
    }

    public bool IsAllowed { get; }
    public string? ViolatedRuleName { get; }
    public IpRestrictionRule? ViolatedRule { get; }
    public string? ValidatedIp { get; }
}