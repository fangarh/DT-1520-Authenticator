using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Auth;

public static class RevokeIntegrationTokenRequestMapper
{
    public static RevokeIntegrationTokenRequest Map(RevokeIntegrationTokenFormRequest request)
    {
        return new RevokeIntegrationTokenRequest
        {
            ClientId = request.ClientId ?? string.Empty,
            ClientSecret = request.ClientSecret ?? string.Empty,
            Token = request.Token ?? string.Empty,
            TokenTypeHint = request.TokenTypeHint,
        };
    }
}
