using System.Text.Json;
using OtpAuth.Api.Auth;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class IssueIntegrationTokenResponseTests
{
    [Fact]
    public void MapResponse_SerializesOAuthTokenFieldsAsSnakeCase()
    {
        var response = IssueIntegrationTokenRequestMapper.MapResponse(new IssuedAccessToken
        {
            AccessToken = "token-one",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = "challenges:read challenges:write",
        });

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"access_token\":\"token-one\"", json, StringComparison.Ordinal);
        Assert.Contains("\"token_type\":\"Bearer\"", json, StringComparison.Ordinal);
        Assert.Contains("\"expires_in\":3600", json, StringComparison.Ordinal);
        Assert.Contains("\"scope\":\"challenges:read challenges:write\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenType", json, StringComparison.Ordinal);
        Assert.DoesNotContain("expiresIn", json, StringComparison.Ordinal);
    }
}
