namespace OtpAuth.Application.Administration;

public interface IAdminUserStore
{
    Task<AdminUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);
}
