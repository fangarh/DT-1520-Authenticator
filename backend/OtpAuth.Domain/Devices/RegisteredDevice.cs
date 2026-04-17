namespace OtpAuth.Domain.Devices;

public sealed record RegisteredDevice
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required string InstallationId { get; init; }

    public string? DeviceName { get; init; }

    public required DeviceStatus Status { get; init; }

    public required DeviceAttestationStatus AttestationStatus { get; init; }

    public string? PushToken { get; init; }

    public string? PublicKey { get; init; }

    public DateTimeOffset? ActivatedUtc { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public required DateTimeOffset LastAuthStateChangedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public DateTimeOffset? BlockedUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public static RegisteredDevice Activate(
        Guid id,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        DevicePlatform platform,
        string installationId,
        string? deviceName,
        string? pushToken,
        string? publicKey,
        DateTimeOffset activatedAtUtc)
    {
        return new RegisteredDevice
        {
            Id = id,
            TenantId = tenantId,
            ApplicationClientId = applicationClientId,
            ExternalUserId = externalUserId,
            Platform = platform,
            InstallationId = installationId,
            DeviceName = deviceName,
            Status = DeviceStatus.Active,
            AttestationStatus = DeviceAttestationStatus.NotProvided,
            PushToken = pushToken,
            PublicKey = publicKey,
            ActivatedUtc = activatedAtUtc,
            LastSeenUtc = activatedAtUtc,
            LastAuthStateChangedUtc = activatedAtUtc,
            CreatedUtc = activatedAtUtc,
        };
    }

    public RegisteredDevice MarkSeen(DateTimeOffset seenAtUtc)
    {
        return this with
        {
            LastSeenUtc = seenAtUtc,
        };
    }

    public RegisteredDevice MarkRevoked(DateTimeOffset revokedAtUtc)
    {
        return Status switch
        {
            DeviceStatus.Revoked => this,
            DeviceStatus.Blocked => this,
            DeviceStatus.Active or DeviceStatus.Pending => this with
            {
                Status = DeviceStatus.Revoked,
                RevokedUtc = revokedAtUtc,
                LastAuthStateChangedUtc = revokedAtUtc,
            },
            _ => throw new InvalidOperationException($"Device '{Id}' has unsupported status '{Status}'."),
        };
    }

    public RegisteredDevice MarkBlocked(DateTimeOffset blockedAtUtc)
    {
        return Status == DeviceStatus.Blocked
            ? this
            : this with
            {
                Status = DeviceStatus.Blocked,
                BlockedUtc = blockedAtUtc,
                LastAuthStateChangedUtc = blockedAtUtc,
            };
    }
}
