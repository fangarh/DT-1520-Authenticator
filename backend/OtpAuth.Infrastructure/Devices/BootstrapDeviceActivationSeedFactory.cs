using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Integrations;

namespace OtpAuth.Infrastructure.Devices;

public sealed class BootstrapDeviceActivationSeedFactory
{
    private readonly IDeviceRefreshTokenHasher _hasher;

    public BootstrapDeviceActivationSeedFactory(IDeviceRefreshTokenHasher hasher)
    {
        _hasher = hasher;
    }

    public BootstrapDeviceActivationSeedMaterial Create(BootstrapOAuthOptions bootstrapOAuthOptions)
    {
        ArgumentNullException.ThrowIfNull(bootstrapOAuthOptions);

        var bootstrapClient = bootstrapOAuthOptions.Clients.FirstOrDefault()
            ?? throw new InvalidOperationException("BootstrapOAuth must define at least one client to seed a bootstrap device activation code.");

        var externalUserId = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_DEVICE_EXTERNAL_USER_ID");
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            throw new InvalidOperationException("OTPAUTH_BOOTSTRAP_DEVICE_EXTERNAL_USER_ID must be configured.");
        }

        var platformRaw = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_DEVICE_PLATFORM");
        var platform = platformRaw?.Trim().ToLowerInvariant() switch
        {
            "android" => DevicePlatform.Android,
            "ios" => DevicePlatform.Ios,
            _ => throw new InvalidOperationException("OTPAUTH_BOOTSTRAP_DEVICE_PLATFORM must be 'android' or 'ios'."),
        };

        var activationCodeSecret = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_DEVICE_ACTIVATION_SECRET");
        if (string.IsNullOrWhiteSpace(activationCodeSecret))
        {
            throw new InvalidOperationException("OTPAUTH_BOOTSTRAP_DEVICE_ACTIVATION_SECRET must be configured.");
        }

        var lifetimeMinutes = 15;
        var activationCodeId = Guid.NewGuid();
        var activationCode = DeviceActivationCodeFormat.Create(activationCodeId, activationCodeSecret.Trim());

        return new BootstrapDeviceActivationSeedMaterial
        {
            ActivationCodeId = activationCodeId,
            TenantId = bootstrapClient.TenantId,
            ApplicationClientId = bootstrapClient.ApplicationClientId,
            ExternalUserId = externalUserId.Trim(),
            Platform = platform,
            ActivationCodeHash = _hasher.Hash(activationCodeSecret.Trim()),
            ActivationCode = activationCode,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(lifetimeMinutes),
        };
    }
}
