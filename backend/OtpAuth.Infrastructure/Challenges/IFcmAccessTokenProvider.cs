namespace OtpAuth.Infrastructure.Challenges;

public interface IFcmAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
