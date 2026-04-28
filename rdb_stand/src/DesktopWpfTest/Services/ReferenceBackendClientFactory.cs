using System.Net.Http;

namespace Dt1520.Authenticator.DesktopWpfTest.Services;

public sealed class ReferenceBackendClientFactory : IReferenceBackendClientFactory
{
    public IReferenceBackendClient Create(Uri backendBaseUrl)
    {
        return new ReferenceBackendClient(new HttpClient
        {
            BaseAddress = backendBaseUrl,
            Timeout = TimeSpan.FromSeconds(30),
        });
    }
}
