namespace OtpAuth.Application.Integrations;

public sealed class IntrospectIntegrationTokenHandler
{
    private readonly IIntegrationClientCredentialsValidator _credentialsValidator;
    private readonly IIntegrationAccessTokenIntrospector _introspector;

    public IntrospectIntegrationTokenHandler(
        IIntegrationClientCredentialsValidator credentialsValidator,
        IIntegrationAccessTokenIntrospector introspector)
    {
        _credentialsValidator = credentialsValidator;
        _introspector = introspector;
    }

    public async Task<IntrospectIntegrationTokenResult> HandleAsync(
        IntrospectIntegrationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return IntrospectIntegrationTokenResult.Failure(
                IntrospectIntegrationTokenErrorCode.ValidationFailed,
                validationError);
        }

        var client = await _credentialsValidator.ValidateAsync(
            request.ClientId,
            request.ClientSecret,
            cancellationToken);
        if (client is null)
        {
            return IntrospectIntegrationTokenResult.Failure(
                IntrospectIntegrationTokenErrorCode.InvalidClient,
                "Client authentication failed.");
        }

        var introspection = await _introspector.IntrospectAsync(request.Token.Trim(), cancellationToken);
        if (!introspection.IsRecognizedToken ||
            !string.Equals(introspection.ClientId, client.ClientId, StringComparison.Ordinal))
        {
            return IntrospectIntegrationTokenResult.Success(IntegrationAccessTokenIntrospectionResult.Unrecognized());
        }

        return IntrospectIntegrationTokenResult.Success(introspection);
    }

    private static string? Validate(IntrospectIntegrationTokenRequest request)
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
