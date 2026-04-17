namespace OtpAuth.Application.Integrations;

public interface IIntegrationClientLifecycleStore
{
    Task<ManagedIntegrationClient?> GetManagedClientByIdAsync(string clientId, CancellationToken cancellationToken);

    Task<bool> RotateSecretAsync(
        string clientId,
        string clientSecretHash,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> SetIsActiveAsync(
        string clientId,
        bool isActive,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);
}
