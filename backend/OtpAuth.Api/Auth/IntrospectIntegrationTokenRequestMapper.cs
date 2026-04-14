using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Auth;

public static class IntrospectIntegrationTokenRequestMapper
{
    public static IntrospectIntegrationTokenRequest Map(IntrospectIntegrationTokenFormRequest request)
    {
        return new IntrospectIntegrationTokenRequest
        {
            ClientId = request.ClientId ?? string.Empty,
            ClientSecret = request.ClientSecret ?? string.Empty,
            Token = request.Token ?? string.Empty,
            TokenTypeHint = request.TokenTypeHint,
        };
    }

    public static IntrospectIntegrationTokenResponse MapResponse(IntegrationAccessTokenIntrospectionResult result)
    {
        return new IntrospectIntegrationTokenResponse
        {
            Active = result.IsActive,
            ClientId = result.IsActive ? result.ClientId : null,
            TenantId = result.IsActive ? result.TenantId : null,
            ApplicationClientId = result.IsActive ? result.ApplicationClientId : null,
            Scope = result.IsActive ? result.Scope : null,
            ExpiresAtUtc = result.IsActive ? result.ExpiresAtUtc : null,
            TokenType = result.IsActive ? "Bearer" : null,
        };
    }
}
