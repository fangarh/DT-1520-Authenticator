namespace OtpAuth.Api.Authentication;

public static class AdminAuthenticationDefaults
{
    public const string AuthenticationScheme = "AdminSession";
    public const string CompositeScheme = "AppAuth";
    public const string AuthenticatedPolicy = "AdminAuthenticated";
    public const string EnrollmentsReadPolicy = "AdminEnrollmentsRead";
    public const string EnrollmentsWritePolicy = "AdminEnrollmentsWrite";
}
