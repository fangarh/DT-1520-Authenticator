namespace Dt1520.Authenticator.ReferenceDesktop;

public sealed record DesktopShellOptions(Uri BackendBaseUrl)
{
    public static DesktopShellOptions FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("RDB_BACKEND_BASE_URL")
            ?? "http://127.0.0.1:5188/";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("RDB_BACKEND_BASE_URL must be an absolute HTTP(S) URL.");
        }

        return new DesktopShellOptions(uri);
    }
}
