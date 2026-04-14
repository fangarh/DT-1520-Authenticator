namespace OtpAuth.Application.Integrations;

public sealed class RevokeIntegrationTokenHandler
{
    private readonly IIntegrationClientCredentialsValidator _credentialsValidator;
    private readonly IIntegrationAccessTokenIntrospector _introspector;
    private readonly IIntegrationAccessTokenRevocationStore _revocationStore;

    public RevokeIntegrationTokenHandler(
        IIntegrationClientCredentialsValidator credentialsValidator,
        IIntegrationAccessTokenIntrospector introspector,
        IIntegrationAccessTokenRevocationStore revocationStore)
    {
        _credentialsValidator = credentialsValidator;
        _introspector = introspector;
        _revocationStore = revocationStore;
    }

    public async Task<RevokeIntegrationTokenResult> HandleAsync(
        RevokeIntegrationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return RevokeIntegrationTokenResult.Failure(
                RevokeIntegrationTokenErrorCode.ValidationFailed,
                validationError);
        }

        var client = await _credentialsValidator.ValidateAsync(
            request.ClientId,
            request.ClientSecret,
            cancellationToken);
        if (client is null)
        {
            return RevokeIntegrationTokenResult.Failure(
                RevokeIntegrationTokenErrorCode.InvalidClient,
                "Client authentication failed.");
        }

        var introspection = await _introspector.IntrospectAsync(request.Token.Trim(), cancellationToken);
        if (!introspection.IsRecognizedToken ||
            !string.Equals(introspection.ClientId, client.ClientId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(introspection.JwtId) ||
            introspection.ExpiresAtUtc is null)
        {
            return RevokeIntegrationTokenResult.Success();
        }

        await _revocationStore.RevokeAsync(
            new RevokedIntegrationAccessToken
            {
                JwtId = introspection.JwtId,
                ClientId = client.ClientId,
                ExpiresAtUtc = introspection.ExpiresAtUtc.Value,
                RevokedAtUtc = DateTimeOffset.UtcNow,
                Reason = "client_revocation",
            },
            cancellationToken);

        return RevokeIntegrationTokenResult.Success();
    }

    private static string? Validate(RevokeIntegrationTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return "ClientId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return "ClientSecret is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return "Token is required.";
        }

        return null;
    }
}
