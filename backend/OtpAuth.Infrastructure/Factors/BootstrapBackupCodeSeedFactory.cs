using OtpAuth.Application.Factors;
using OtpAuth.Infrastructure.Integrations;

namespace OtpAuth.Infrastructure.Factors;

public sealed class BootstrapBackupCodeSeedFactory
{
    public BootstrapBackupCodeSeedMaterial Create(BootstrapOAuthOptions bootstrapOAuthOptions)
    {
        ArgumentNullException.ThrowIfNull(bootstrapOAuthOptions);

        var externalUserId = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_BACKUP_CODES_EXTERNAL_USER_ID");
        var rawCodes = Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_BACKUP_CODES");

        if (string.IsNullOrWhiteSpace(externalUserId) || string.IsNullOrWhiteSpace(rawCodes))
        {
            throw new InvalidOperationException(
                "OTPAUTH_BOOTSTRAP_BACKUP_CODES_EXTERNAL_USER_ID and OTPAUTH_BOOTSTRAP_BACKUP_CODES are required for bootstrap backup code seeding.");
        }

        var tenantId = TryReadGuidFromEnvironment("OTPAUTH_BOOTSTRAP_BACKUP_CODES_TENANT_ID");
        var applicationClientId = TryReadGuidFromEnvironment("OTPAUTH_BOOTSTRAP_BACKUP_CODES_APPLICATION_CLIENT_ID");
        var username = NormalizeOptional(Environment.GetEnvironmentVariable("OTPAUTH_BOOTSTRAP_BACKUP_CODES_USERNAME"));

        if (tenantId is null || applicationClientId is null)
        {
            var bootstrapClient = bootstrapOAuthOptions.Clients.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Bootstrap OAuth client metadata is required when backup code tenant/application env vars are not provided.");

            tenantId ??= bootstrapClient.TenantId;
            applicationClientId ??= bootstrapClient.ApplicationClientId;
        }

        var codes = rawCodes
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCode)
            .ToArray();

        if (codes.Length == 0)
        {
            throw new InvalidOperationException("OTPAUTH_BOOTSTRAP_BACKUP_CODES must contain at least one backup code.");
        }

        if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
        {
            throw new InvalidOperationException("OTPAUTH_BOOTSTRAP_BACKUP_CODES must contain unique backup codes after normalization.");
        }

        return new BootstrapBackupCodeSeedMaterial
        {
            TenantId = tenantId.Value,
            ApplicationClientId = applicationClientId.Value,
            ExternalUserId = externalUserId.Trim(),
            Username = username,
            Codes = codes,
        };
    }

    private static string NormalizeCode(string rawCode)
    {
        if (!BackupCodeFormat.TryNormalize(rawCode, out var normalizedCode, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        return normalizedCode;
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
