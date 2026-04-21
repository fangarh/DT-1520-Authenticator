using System.Text;
using Google.Apis.Auth.OAuth2;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class GoogleCredentialFcmAccessTokenProvider : IFcmAccessTokenProvider
{
    private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly SemaphoreSlim _credentialLock = new(1, 1);
    private readonly FcmPushChallengeDeliveryGatewayOptions _options;
    private GoogleCredential? _credential;

    public GoogleCredentialFcmAccessTokenProvider(PushChallengeDeliveryGatewayOptions options)
    {
        _options = options.Fcm;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var credential = await GetCredentialAsync(cancellationToken);
        return await ((ITokenAccess)credential).GetAccessTokenForRequestAsync(null, cancellationToken);
    }

    private async Task<GoogleCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        if (_credential is not null)
        {
            return _credential;
        }

        await _credentialLock.WaitAsync(cancellationToken);
        try
        {
            if (_credential is not null)
            {
                return _credential;
            }

            var credentialJson = _options.GetServiceAccountJson();
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(credentialJson));
            var credential = GoogleCredential.FromStream(stream);
            if (credential.IsCreateScopedRequired)
            {
                credential = credential.CreateScoped(FirebaseMessagingScope);
            }

            _credential = credential;
            return credential;
        }
        finally
        {
            _credentialLock.Release();
        }
    }
}
