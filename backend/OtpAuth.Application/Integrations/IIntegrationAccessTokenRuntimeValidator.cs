using System.Security.Claims;

namespace OtpAuth.Application.Integrations;

public interface IIntegrationAccessTokenRuntimeValidator
{
    Task<IntegrationAccessTokenRuntimeValidationResult> ValidateAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
