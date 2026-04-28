using System.Security.Cryptography;

namespace OtpAuth.Application.Administration;

public interface IAdminTenantDirectoryIdGenerator
{
    Guid NewTenantId();

    Guid NewApplicationClientId();

    string NewIntegrationClientId(string tenantDisplayName, string applicationDisplayName, string integrationClientDisplayName);
}

public sealed class AdminTenantDirectoryIdGenerator : IAdminTenantDirectoryIdGenerator
{
    public Guid NewTenantId()
    {
        return Guid.NewGuid();
    }

    public Guid NewApplicationClientId()
    {
        return Guid.NewGuid();
    }

    public string NewIntegrationClientId(
        string tenantDisplayName,
        string applicationDisplayName,
        string integrationClientDisplayName)
    {
        var prefix = AdminTenantDirectoryValidation.CreateSlugCandidate(
            $"{tenantDisplayName}-{applicationDisplayName}-{integrationClientDisplayName}",
            "integration-client");
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var candidate = $"{prefix}-{suffix}";
        return candidate.Length <= 200
            ? candidate
            : $"{candidate[..191].Trim('-', '_', '.')}-{suffix}";
    }
}
