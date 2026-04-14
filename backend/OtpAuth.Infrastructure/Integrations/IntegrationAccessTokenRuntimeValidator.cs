using System.Security.Claims;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class IntegrationAccessTokenRuntimeValidator : IIntegrationAccessTokenRuntimeValidator
{
    private readonly IIntegrationClientStore _clientStore;
    private readonly IIntegrationAccessTokenRevocationStore _revocationStore;

    public IntegrationAccessTokenRuntimeValidator(
        IIntegrationClientStore clientStore,
        IIntegrationAccessTokenRevocationStore revocationStore)
    {
        _clientStore = clientStore;
        _revocationStore = revocationStore;
    }

    public async Task<IntegrationAccessTokenRuntimeValidationResult> ValidateAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clientId = principal.FindFirst("client_id")?.Value;
        var tenantId = principal.FindFirst("tenant_id")?.Value;
        var applicationClientId = principal.FindFirst("application_client_id")?.Value;
        var jwtId = principal.FindFirst("jti")?.Value;

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(jwtId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId) ||
            !Guid.TryParse(applicationClientId, out var parsedApplicationClientId))
        {
            return IntegrationAccessTokenRuntimeValidationResult.Failure("Integration access token is missing required claims.");
        }

        var client = await _clientStore.GetByClientIdAsync(clientId, cancellationToken);
        if (client is null)
        {
            return IntegrationAccessTokenRuntimeValidationResult.Failure("Integration client is inactive or unknown.");
        }

        if (client.TenantId != parsedTenantId || client.ApplicationClientId != parsedApplicationClientId)
        {
            return IntegrationAccessTokenRuntimeValidationResult.Failure("Integration access token claims do not match the active client.");
        }

        if (await _revocationStore.IsRevokedAsync(jwtId, cancellationToken))
        {
            return IntegrationAccessTokenRuntimeValidationResult.Failure("Integration access token has been revoked.");
        }

        return IntegrationAccessTokenRuntimeValidationResult.Success();
    }
}
