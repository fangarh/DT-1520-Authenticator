namespace OtpAuth.Application.Integrations;

public interface IClientSecretHasher
{
    string Hash(string secret);

    bool Verify(string secret, string secretHash);
}
