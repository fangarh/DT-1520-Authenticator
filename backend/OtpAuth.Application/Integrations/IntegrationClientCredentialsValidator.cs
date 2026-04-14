namespace OtpAuth.Application.Integrations;

public sealed class IntegrationClientCredentialsValidator : IIntegrationClientCredentialsValidator
{
    private readonly IIntegrationClientStore _clientStore;
    private readonly IClientSecretHasher _clientSecretHasher;

    public IntegrationClientCredentialsValidator(
        IIntegrationClientStore clientStore,
        IClientSecretHasher clientSecretHasher)
    {
        _clientStore = clientStore;
        _clientSecretHasher = clientSecretHasher;
    }

    public async Task<IntegrationClient?> ValidateAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var client = await _clientStore.GetByClientIdAsync(clientId.Trim(), cancellationToken);
        if (client is null)
        {
            return null;
        }

        return _clientSecretHasher.Verify(clientSecret, client.ClientSecretHash)
            ? client
            : null;
    }
}
