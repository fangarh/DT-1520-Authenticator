namespace OtpAuth.Api.Authentication;

public static class AdminAuthenticationDefaults
{
    public const string AuthenticationScheme = "AdminSession";
    public const string CompositeScheme = "AppAuth";
    public const string AuthenticatedPolicy = "AdminAuthenticated";
    public const string DevicesReadPolicy = "AdminDevicesRead";
    public const string DevicesWritePolicy = "AdminDevicesWrite";
    public const string EnrollmentsReadPolicy = "AdminEnrollmentsRead";
    public const string EnrollmentsWritePolicy = "AdminEnrollmentsWrite";
    public const string IntegrationClientsReadPolicy = "AdminIntegrationClientsRead";
    public const string IntegrationClientsWritePolicy = "AdminIntegrationClientsWrite";
    public const string TenantsReadPolicy = "AdminTenantsRead";
    public const string TenantsWritePolicy = "AdminTenantsWrite";
    public const string WebhooksReadPolicy = "AdminWebhooksRead";
    public const string WebhooksWritePolicy = "AdminWebhooksWrite";
}
