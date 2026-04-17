namespace OtpAuth.Application.Administration;

public interface IAdminApplicationClientResolver
{
    Task<AdminApplicationClientResolutionResult> ResolveAsync(
        Guid tenantId,
        Guid? requestedApplicationClientId,
        CancellationToken cancellationToken);
}
