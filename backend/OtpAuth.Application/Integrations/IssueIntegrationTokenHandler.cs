namespace OtpAuth.Application.Integrations;

public sealed class IssueIntegrationTokenHandler
{
    private readonly IIntegrationClientCredentialsValidator _credentialsValidator;
    private readonly IIntegrationAccessTokenIssuer _accessTokenIssuer;

    public IssueIntegrationTokenHandler(
        IIntegrationClientCredentialsValidator credentialsValidator,
        IIntegrationAccessTokenIssuer accessTokenIssuer)
    {
        _credentialsValidator = credentialsValidator;
        _accessTokenIssuer = accessTokenIssuer;
    }

    public async Task<IssueIntegrationTokenResult> HandleAsync(
        IssueIntegrationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return IssueIntegrationTokenResult.Failure(
                IssueIntegrationTokenErrorCode.ValidationFailed,
                validationError);
        }

        var client = await _credentialsValidator.ValidateAsync(
            request.ClientId,
            request.ClientSecret,
            cancellationToken);
        if (client is null)
        {
            return IssueIntegrationTokenResult.Failure(
                IssueIntegrationTokenErrorCode.InvalidClient,
                "Client authentication failed.");
        }

        var requestedScopes = ParseRequestedScopes(request.Scope);
        if (requestedScopes.Count == 0)
        {
            requestedScopes = client.AllowedScopes.ToArray();
        }

        if (requestedScopes.Any(scope => !client.AllowedScopes.Contains(scope, StringComparer.Ordinal)))
        {
            return IssueIntegrationTokenResult.Failure(
                IssueIntegrationTokenErrorCode.InvalidScope,
                "Requested scope is not allowed for the client.");
        }

        var issuedToken = await _accessTokenIssuer.IssueAsync(client, requestedScopes, cancellationToken);
        return IssueIntegrationTokenResult.Success(issuedToken);
    }

    private static string? Validate(IssueIntegrationTokenRequest request)
    {
        if (!string.Equals(request.GrantType?.Trim(), "client_credentials", StringComparison.Ordinal))
        {
            return "Grant type 'client_credentials' is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return "ClientId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return "ClientSecret is required.";
        }

        return null;
    }

    private static IReadOnlyCollection<string> ParseRequestedScopes(string? rawScope)
    {
        if (string.IsNullOrWhiteSpace(rawScope))
        {
            return Array.Empty<string>();
        }

        return rawScope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
