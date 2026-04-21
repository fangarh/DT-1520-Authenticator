using OtpAuth.Application.Devices;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Devices;

namespace OtpAuth.Infrastructure.Tests.Devices;

internal sealed class InMemoryDeviceRegistryStore : IDeviceRegistryStore
{
    private readonly Dictionary<Guid, DeviceActivationCodeArtifact> _activationCodes = [];
    private readonly Dictionary<Guid, RegisteredDevice> _devices = [];
    private readonly Dictionary<Guid, DeviceRefreshTokenRecord> _refreshTokens = [];
    private readonly List<WebhookSubscription> _subscriptions = [];
    private readonly List<WebhookEventDelivery> _webhookDeliveries = [];
    private readonly IDeviceRefreshTokenHasher _hasher = new Pbkdf2DeviceRefreshTokenHasher();

    public Task<DeviceActivationCodeArtifact?> GetActivationCodeByIdAsync(Guid activationCodeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _activationCodes.TryGetValue(activationCodeId, out var activationCode);
        return Task.FromResult(activationCode);
    }

    public Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _devices.TryGetValue(deviceId, out var device);
        return Task.FromResult(device);
    }

    public Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, Guid tenantId, Guid applicationClientId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _devices.TryGetValue(deviceId, out var device);
        if (device is null || device.TenantId != tenantId || device.ApplicationClientId != applicationClientId)
        {
            return Task.FromResult<RegisteredDevice?>(null);
        }

        return Task.FromResult<RegisteredDevice?>(device);
    }

    public Task<RegisteredDevice?> GetActiveByInstallationAsync(Guid tenantId, Guid applicationClientId, string installationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = _devices.Values.SingleOrDefault(candidate =>
            candidate.TenantId == tenantId &&
            candidate.ApplicationClientId == applicationClientId &&
            string.Equals(candidate.InstallationId, installationId, StringComparison.Ordinal) &&
            candidate.Status == DeviceStatus.Active);

        return Task.FromResult(device);
    }

    public Task<IReadOnlyCollection<RegisteredDevice>> ListActiveByExternalUserAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyCollection<RegisteredDevice> devices = _devices.Values
            .Where(candidate =>
                candidate.TenantId == tenantId &&
                candidate.ApplicationClientId == applicationClientId &&
                string.Equals(candidate.ExternalUserId, externalUserId, StringComparison.Ordinal) &&
                candidate.Status == DeviceStatus.Active)
            .ToArray();

        return Task.FromResult(devices);
    }

    public Task<DeviceRefreshTokenRecord?> GetRefreshTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _refreshTokens.TryGetValue(tokenId, out var token);
        return Task.FromResult(token);
    }

    public Task<bool> ActivateAsync(
        RegisteredDevice device,
        DeviceRefreshTokenRecord refreshToken,
        Guid activationCodeId,
        DateTimeOffset activatedAtUtc,
        DeviceLifecycleSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_activationCodes.TryGetValue(activationCodeId, out var activationCode) ||
            activationCode.ConsumedUtc.HasValue ||
            activationCode.ExpiresUtc <= activatedAtUtc)
        {
            return Task.FromResult(false);
        }

        _activationCodes[activationCodeId] = activationCode with
        {
            ConsumedUtc = activatedAtUtc,
        };
        _devices[device.Id] = device;
        _refreshTokens[refreshToken.TokenId] = refreshToken;
        QueueWebhookDeliveries(sideEffects?.WebhookEvent);
        return Task.FromResult(true);
    }

    public Task<bool> RotateRefreshTokenAsync(
        DeviceRefreshRotation rotation,
        Guid deviceId,
        DateTimeOffset lastSeenUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_refreshTokens.TryGetValue(rotation.CurrentTokenId, out var currentToken) ||
            currentToken.ConsumedUtc.HasValue ||
            currentToken.RevokedUtc.HasValue ||
            currentToken.ExpiresUtc <= rotation.RotatedAtUtc)
        {
            return Task.FromResult(false);
        }

        _refreshTokens[rotation.CurrentTokenId] = currentToken with
        {
            ConsumedUtc = rotation.RotatedAtUtc,
            ReplacedByTokenId = rotation.ReplacedByTokenId,
        };
        _refreshTokens[rotation.NewToken.TokenId] = rotation.NewToken;

        if (_devices.TryGetValue(deviceId, out var device))
        {
            _devices[deviceId] = device.MarkSeen(lastSeenUtc);
        }

        return Task.FromResult(true);
    }

    public Task<bool> RevokeDeviceAsync(
        RegisteredDevice device,
        DateTimeOffset revokedAtUtc,
        DeviceLifecycleSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_devices.ContainsKey(device.Id))
        {
            return Task.FromResult(false);
        }

        _devices[device.Id] = device;
        foreach (var token in _refreshTokens.Values.Where(token => token.DeviceId == device.Id).ToArray())
        {
            _refreshTokens[token.TokenId] = token with
            {
                RevokedUtc = revokedAtUtc,
            };
        }

        QueueWebhookDeliveries(sideEffects?.WebhookEvent);
        return Task.FromResult(true);
    }

    public Task<bool> BlockDeviceAsync(
        RegisteredDevice device,
        DateTimeOffset blockedAtUtc,
        DeviceLifecycleSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_devices.ContainsKey(device.Id))
        {
            return Task.FromResult(false);
        }

        _devices[device.Id] = device;
        foreach (var token in _refreshTokens.Values.Where(token => token.DeviceId == device.Id).ToArray())
        {
            _refreshTokens[token.TokenId] = token with
            {
                RevokedUtc = blockedAtUtc,
            };
        }

        QueueWebhookDeliveries(sideEffects?.WebhookEvent);
        return Task.FromResult(true);
    }

    public SeededActivationCode SeedActivationCode(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        DevicePlatform platform,
        string secret,
        DateTimeOffset? expiresUtc = null)
    {
        var activationCodeId = Guid.NewGuid();
        var seeded = new SeededActivationCode
        {
            ActivationCodeId = activationCodeId,
            PlaintextCode = DeviceActivationCodeFormat.Create(activationCodeId, secret),
            ExternalUserId = externalUserId,
            Platform = platform,
            ExpiresUtc = expiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(10),
        };

        _activationCodes[activationCodeId] = new DeviceActivationCodeArtifact
        {
            ActivationCodeId = activationCodeId,
            TenantId = tenantId,
            ApplicationClientId = applicationClientId,
            ExternalUserId = externalUserId,
            Platform = platform,
            CodeHash = _hasher.Hash(secret),
            ExpiresUtc = seeded.ExpiresUtc,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        return seeded;
    }

    public SeededDevice SeedActiveDevice(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string installationId,
        string refreshTokenSecret = "bootstrap-secret",
        DateTimeOffset? tokenExpiresUtc = null,
        DeviceStatus status = DeviceStatus.Active,
        string? pushToken = "push-token",
        Guid? deviceId = null)
    {
        var device = RegisteredDevice.Activate(
            deviceId ?? Guid.NewGuid(),
            tenantId,
            applicationClientId,
            externalUserId,
            DevicePlatform.Android,
            installationId,
            "Pixel",
            pushToken,
            null,
            DateTimeOffset.UtcNow) with
        {
            Status = status,
            RevokedUtc = status == DeviceStatus.Revoked ? DateTimeOffset.UtcNow : null,
            BlockedUtc = status == DeviceStatus.Blocked ? DateTimeOffset.UtcNow : null,
        };
        _devices[device.Id] = device;

        var tokenId = Guid.NewGuid();
        var tokenRecord = new DeviceRefreshTokenRecord
        {
            TokenId = tokenId,
            DeviceId = device.Id,
            TokenFamilyId = Guid.NewGuid(),
            TokenHash = _hasher.Hash(refreshTokenSecret),
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = tokenExpiresUtc ?? DateTimeOffset.UtcNow.AddDays(30),
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        _refreshTokens[tokenId] = tokenRecord;

        return new SeededDevice
        {
            Device = device,
            RefreshTokenRecord = tokenRecord,
            PlaintextRefreshToken = DeviceRefreshTokenFormat.Create(tokenId, refreshTokenSecret),
        };
    }

    public void SeedWebhookSubscription(WebhookSubscription subscription)
    {
        _subscriptions.Add(subscription);
    }

    public IReadOnlyCollection<WebhookEventDelivery> GetWebhookDeliveries()
    {
        return _webhookDeliveries.ToArray();
    }

    public IReadOnlyCollection<RegisteredDevice> ListByTenantAndExternalUser(Guid tenantId, string externalUserId)
    {
        return _devices.Values
            .Where(candidate =>
                candidate.TenantId == tenantId &&
                string.Equals(candidate.ExternalUserId, externalUserId, StringComparison.Ordinal))
            .ToArray();
    }

    private void QueueWebhookDeliveries(WebhookEventPublication? publication)
    {
        if (publication is null)
        {
            return;
        }

        foreach (var subscription in _subscriptions.Where(subscription =>
                     subscription.IsActive &&
                     subscription.TenantId == publication.TenantId &&
                     subscription.ApplicationClientId == publication.ApplicationClientId &&
                     subscription.EventTypes.Contains(publication.EventType, StringComparer.Ordinal)))
        {
            _webhookDeliveries.Add(new WebhookEventDelivery
            {
                DeliveryId = Guid.NewGuid(),
                SubscriptionId = subscription.SubscriptionId,
                TenantId = publication.TenantId,
                ApplicationClientId = publication.ApplicationClientId,
                EndpointUrl = subscription.EndpointUrl,
                EventId = publication.EventId,
                EventType = publication.EventType,
                OccurredAtUtc = publication.OccurredAtUtc,
                ResourceType = publication.ResourceType,
                ResourceId = publication.ResourceId,
                PayloadJson = publication.PayloadJson,
                Status = WebhookEventDeliveryStatus.Queued,
                AttemptCount = 0,
                NextAttemptUtc = publication.OccurredAtUtc,
                CreatedUtc = publication.OccurredAtUtc,
            });
        }
    }

    public sealed record SeededActivationCode
    {
        public required Guid ActivationCodeId { get; init; }

        public required string PlaintextCode { get; init; }

        public required string ExternalUserId { get; init; }

        public required DevicePlatform Platform { get; init; }

        public required DateTimeOffset ExpiresUtc { get; init; }
    }

    public sealed record SeededDevice
    {
        public required RegisteredDevice Device { get; init; }

        public required DeviceRefreshTokenRecord RefreshTokenRecord { get; init; }

        public required string PlaintextRefreshToken { get; init; }
    }
}
