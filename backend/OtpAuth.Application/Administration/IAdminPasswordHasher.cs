namespace OtpAuth.Application.Administration;

public interface IAdminPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
