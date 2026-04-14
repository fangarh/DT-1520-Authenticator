using OtpAuth.Infrastructure.Integrations;

namespace OtpAuth.Infrastructure.Factors;

public sealed class BootstrapTotpEnrollmentSeedFactory
{
    public BootstrapTotpEnrollmentSeedMaterial Create(BootstrapOAuthOptions bootstrapOAuthOptions)
    {
        ArgumentNullException.ThrowIfNull(bootstrapOAuthOptions);

        var externalUserId = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID");
        var secretBase64 = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64");

        if (string.IsNullOrWhiteSpace(externalUserId) || string.IsNullOrWhiteSpace(secretBase64))
        {
            throw new InvalidOperationException(
                "OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID and OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64 are required for bootstrap TOTP enrollment seeding.");
        }

        byte[] secret;
        try
        {
            secret = Convert.FromBase64String(secretBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64 must be a valid base64 string.",
                exception);
        }

        if (secret.Length < 16)
        {
            throw new InvalidOperationException(
                "OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64 must decode to at least 16 bytes.");
        }

        var tenantId = TryReadGuidFromEnvironment("OTPAUTH_BOOTSTRAP_TOTP_TENANT_ID");
        var applicationClientId = TryReadGuidFromEnvironment("OTPAUTH_BOOTSTRAP_TOTP_APPLICATION_CLIENT_ID");
        var username = NormalizeOptional(Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_USERNAME"));

        if (tenantId is null || applicationClientId is null)
        {
            var bootstrapClient = bootstrapOAuthOptions.Clients.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Bootstrap OAuth client metadata is required when TOTP tenant/application env vars are not provided.");

            tenantId ??= bootstrapClient.TenantId;
            applicationClientId ??= bootstrapClient.ApplicationClientId;
        }

        return new BootstrapTotpEnrollmentSeedMaterial
        {
            TenantId = tenantId.Value,
            ApplicationClientId = applicationClientId.Value,
            ExternalUserId = externalUserId.Trim(),
            Username = username,
            Secret = secret,
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
        };
    }

    private static Guid? TryReadGuidFromEnvironment(string variableName)
    {
        var rawValue = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (!Guid.TryParse(rawValue, out var value))
        {
            throw new InvalidOperationException($"Environment variable '{variableName}' must be a valid GUID.");
        }

        return value;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
