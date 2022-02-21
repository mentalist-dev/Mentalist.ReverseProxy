namespace Mentalist.ReverseProxy.Limits;

public class IpRestrictionValidationResult
{
    public IpRestrictionValidationResult(bool isAllowed, string? violatedRuleName = null, IpRestrictionRule? violatedRule = null)
    {
        IsAllowed = isAllowed;
        ViolatedRuleName = violatedRuleName;
        ViolatedRule = violatedRule;
    }

    public bool IsAllowed { get; }
    public string? ViolatedRuleName { get; }
    public IpRestrictionRule? ViolatedRule { get; }
}