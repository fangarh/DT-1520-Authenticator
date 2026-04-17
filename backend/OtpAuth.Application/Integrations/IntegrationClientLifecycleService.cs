using System.Security.Cryptography;

namespace OtpAuth.Application.Integrations;

public sealed class IntegrationClientLifecycleService
{
    private readonly IIntegrationClientLifecycleStore _lifecycleStore;
    private readonly IClientSecretHasher _clientSecretHasher;

    public IntegrationClientLifecycleService(
        IIntegrationClientLifecycleStore lifecycleStore,
        IClientSecretHasher clientSecretHasher)
    {
        _lifecycleStore = lifecycleStore;
        _clientSecretHasher = clientSecretHasher;
    }

    public async Task<RotateIntegrationClientSecretResult> RotateSecretAsync(
        string clientId,
        string? explicitClientSecret,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedClientId = NormalizeClientId(clientId);
        if (normalizedClientId is null)
        {
            return RotateIntegrationClientSecretResult.Failure("ClientId is required.");
        }

        var client = await _lifecycleStore.GetManagedClientByIdAsync(normalizedClientId, cancellationToken);
        if (client is null)
        {
            return RotateIntegrationClientSecretResult.Failure("Integration client was not found.");
        }

        var newClientSecret = string.IsNullOrWhiteSpace(explicitClientSecret)
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            : explicitClientSecret.Trim();

        var rotatedAtUtc = DateTimeOffset.UtcNow;
        var clientSecretHash = _clientSecretHasher.Hash(newClientSecret);
        var updated = await _lifecycleStore.RotateSecretAsync(
            normalizedClientId,
            clientSecretHash,
            rotatedAtUtc,
            cancellationToken);

        return updated
            ? RotateIntegrationClientSecretResult.Success(newClientSecret, rotatedAtUtc)
            : RotateIntegrationClientSecretResult.Failure("Integration client secret rotation failed.");
    }

    public async Task<SetIntegrationClientActiveStateResult> SetIsActiveAsync(
        string clientId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedClientId = NormalizeClientId(clientId);
        if (normalizedClientId is null)
        {
            return SetIntegrationClientActiveStateResult.Failure("ClientId is required.");
        }

        var client = await _lifecycleStore.GetManagedClientByIdAsync(normalizedClientId, cancellationToken);
        if (client is null)
        {
            return SetIntegrationClientActiveStateResult.Failure("Integration client was not found.");
        }

        if (client.IsActive == isActive)
        {
            return SetIntegrationClientActiveStateResult.Success(
                isActive,
                client.LastAuthStateChangedUtc,
                wasStateChanged: false);
        }

        var changedAtUtc = DateTimeOffset.UtcNow;
        var updated = await _lifecycleStore.SetIsActiveAsync(
            normalizedClientId,
            isActive,
            changedAtUtc,
            cancellationToken);

        return updated
            ? SetIntegrationClientActiveStateResult.Success(isActive, changedAtUtc, wasStateChanged: true)
            : SetIntegrationClientActiveStateResult.Failure("Integration client state update failed.");
    }

    private static string? NormalizeClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        return clientId.Trim();
    }
}
