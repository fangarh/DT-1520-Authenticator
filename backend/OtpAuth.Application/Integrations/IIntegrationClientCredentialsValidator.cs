namespace OtpAuth.Application.Integrations;

public interface IIntegrationClientCredentialsValidator
{
    Task<IntegrationClient?> ValidateAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken);
}
