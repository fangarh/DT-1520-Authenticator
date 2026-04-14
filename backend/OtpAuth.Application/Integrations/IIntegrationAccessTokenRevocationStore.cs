namespace OtpAuth.Application.Integrations;

public interface IIntegrationAccessTokenRevocationStore
{
    Task<bool> IsRevokedAsync(string jwtId, CancellationToken cancellationToken);

    Task RevokeAsync(
        RevokedIntegrationAccessToken token,
        CancellationToken cancellationToken);
}
