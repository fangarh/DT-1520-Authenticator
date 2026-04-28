using System.Security.Cryptography;

namespace OtpAuth.Application.Administration;

public interface IAdminIntegrationClientSecretGenerator
{
    string Generate();
}

public sealed class AdminIntegrationClientSecretGenerator : IAdminIntegrationClientSecretGenerator
{
    private const int SecretBytes = 32;

    public string Generate()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
    }
}
