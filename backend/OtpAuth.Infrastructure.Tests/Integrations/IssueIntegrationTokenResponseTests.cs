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

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.Equal("token-one", accessToken.GetString());
        Assert.Equal("Bearer", root.GetProperty("token_type").GetString());
        Assert.Equal(3600, root.GetProperty("expires_in").GetInt32());
        Assert.Equal("challenges:read challenges:write", root.GetProperty("scope").GetString());
        Assert.False(root.TryGetProperty("accessToken", out _));
        Assert.False(root.TryGetProperty("tokenType", out _));
        Assert.False(root.TryGetProperty("expiresIn", out _));
    }
}
