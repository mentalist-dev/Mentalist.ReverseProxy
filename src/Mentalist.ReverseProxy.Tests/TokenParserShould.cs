using Mentalist.ReverseProxy.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Mentalist.ReverseProxy.Tests;

public class TokenParserShould
{
    private readonly ITestOutputHelper _output;

    public TokenParserShould(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Parse()
    {
        var jwtToken =
            "eyJhbGciOiJSUzUxMiIsImtpZCI6IlhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWCIsInR5cCI6ImF0K2p3dCIsIng1dCI6IlhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWCJ9.eyJuYmYiOjE2NTc4MDg3NDAsImV4cCI6MTY1NzgyMzE0MCwiaXNzIjoiaHR0cHM6Ly9sb2dpbi5leGFtcGxlLmNvbSIsImF1ZCI6WyJodHRwczovL3Jlc291cmNlMS5leGFtcGxlLmNvbSIsImh0dHBzOi8vcmVzb3VyY2UyLmV4YW1wbGUuY29tIiwiaHR0cHM6Ly9yZXNvdXJjZTMuZXhhbXBsZS5jb20iXSwiY2xpZW50X2lkIjoiYXBwIiwic3ViIjoiYS51c2VyQGV4YW1wbGUuY29tIiwiYXV0aF90aW1lIjoxNjU3ODA4NzM5LCJpZHAiOiJodHRwczovL2xvZ2luLmV4YW1wbGUuY29tIiwibmFtZSI6IkZpcnN0bmFtZSBMYXN0bmFtZSIsInVzZXJJZCI6IjczY2NjMmU1LTA0ZDAtNDE4Ni1hOTMxLTNlOGIxZTc0MTcxOSIsInJvbGUiOlsiQmlsbGluZyIsIkN1c3RvbWVycyJdLCJzZXNzaW9uIjoiMDQ2OWZlYmYtZThjMC00MGFjLTk1OWEtOTliMzc2ZDYzMjJhIiwianRpIjoiWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFgiLCJzaWQiOiJYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWCIsImlhdCI6MTY1NzgwODc0MCwic2NvcGUiOlsib3BlbmlkIiwicHJvZmlsZSIsInJlc291cmNlMSIsInJlc291cmNlMiIsInJlc291cmNlMyJdLCJhbXIiOlsiTWljcm9zb2Z0Il19.eyJzaWduYXR1cmUiOiAiaW52YWxpZCJ9";

        var parsed = TokenParser.Parse(jwtToken)!;
        Assert.NotNull(parsed);

        Assert.True(parsed.ContainsKey("nbf"));
        Assert.True(parsed.ContainsKey("exp"));
        Assert.True(parsed.ContainsKey("iss"));
        Assert.True(parsed.ContainsKey("aud"));
        Assert.True(parsed.ContainsKey("client_id"));

        Assert.Equal("app", parsed["client_id"]);
    }

    /*
    {"alg":"RS512","kid":"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX","typ":"at+jwt","x5t":"XXXXXXXXXXXXXXXXXXXXXXXXXXX"}
    {
        "nbf": 1657808740,
        "exp": 1657823140,
        "iss": "https://login.example.com",
        "aud": [
            "https://resource1.example.com",
            "https://resource2.example.com",
            "https://resource3.example.com"
        ],
        "client_id": "app",
        "sub": "a.user@example.com",
        "auth_time": 1657808739,
        "idp": "https://login.example.com",
        "name": "Firstname Lastname",
        "userId": "73ccc2e5-04d0-4186-a931-3e8b1e741719",
        "role": [
            "Billing",
            "Customers"
        ],
        "session": "0469febf-e8c0-40ac-959a-99b376d6322a",
        "jti": "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
        "sid": "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
        "iat": 1657808740,
        "scope": [
            "openid",
            "profile",
            "resource1",
            "resource2",
            "resource3"
        ],
        "amr": [
            "Microsoft"
        ]
    }
    */
}