namespace OtpAuth.Application.Integrations;

public interface IIntegrationAccessTokenIssuer
{
    Task<IssuedAccessToken> IssueAsync(
        IntegrationClient client,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken);
}
