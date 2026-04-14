using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Auth;

public static class IssueIntegrationTokenRequestMapper
{
    public static IssueIntegrationTokenRequest Map(IssueIntegrationTokenFormRequest request)
    {
        return new IssueIntegrationTokenRequest
        {
            GrantType = request.GrantType,
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            Scope = request.Scope,
        };
    }

    public static IssueIntegrationTokenResponse MapResponse(IssuedAccessToken token)
    {
        return new IssueIntegrationTokenResponse
        {
            AccessToken = token.AccessToken,
            TokenType = token.TokenType,
            ExpiresIn = token.ExpiresIn,
            Scope = token.Scope,
        };
    }
}
