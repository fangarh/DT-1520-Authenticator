namespace OtpAuth.Application.Integrations;

public interface IIntegrationAccessTokenIntrospector
{
    Task<IntegrationAccessTokenIntrospectionResult> IntrospectAsync(
        string token,
        CancellationToken cancellationToken);
}
