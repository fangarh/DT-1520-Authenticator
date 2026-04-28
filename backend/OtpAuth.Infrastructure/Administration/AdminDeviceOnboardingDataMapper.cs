using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Administration;

internal sealed record AdminDeviceOnboardingPersistenceModel
{
    public required Guid ActivationCodeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public DateTimeOffset? ConsumedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

internal static class AdminDeviceOnboardingDataMapper
{
    public static AdminDeviceOnboardingView ToDomainModel(
        AdminDeviceOnboardingPersistenceModel source,
        DateTimeOffset nowUtc)
    {
        return new AdminDeviceOnboardingView
        {
            ActivationCodeId = source.ActivationCodeId,
            TenantId = source.TenantId,
            ApplicationClientId = source.ApplicationClientId,
            ExternalUserId = source.ExternalUserId,
            Platform = source.Platform,
            Status = GetStatus(source, nowUtc),
            ExpiresUtc = source.ExpiresUtc,
            ConsumedUtc = source.ConsumedUtc,
            RevokedUtc = source.RevokedUtc,
            CreatedUtc = source.CreatedUtc,
        };
    }

    private static AdminDeviceOnboardingStatus GetStatus(
        AdminDeviceOnboardingPersistenceModel source,
        DateTimeOffset nowUtc)
    {
        if (source.RevokedUtc.HasValue)
        {
            return AdminDeviceOnboardingStatus.Revoked;
        }

        if (source.ConsumedUtc.HasValue)
        {
            return AdminDeviceOnboardingStatus.Consumed;
        }

        return source.ExpiresUtc <= nowUtc
            ? AdminDeviceOnboardingStatus.Expired
            : AdminDeviceOnboardingStatus.Pending;
    }
}
