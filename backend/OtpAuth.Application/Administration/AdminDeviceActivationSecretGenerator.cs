using System.Security.Cryptography;

namespace OtpAuth.Application.Administration;

public interface IAdminDeviceActivationSecretGenerator
{
    string Generate();
}

public sealed class AdminDeviceActivationSecretGenerator : IAdminDeviceActivationSecretGenerator
{
    private const int SecretBytes = 32;

    public string Generate()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
    }
}
