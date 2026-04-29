namespace Dt1520.Authenticator.DesktopWpfTest.Services;

public interface IReferenceBackendClientFactory
{
    IReferenceBackendClient Create(Uri backendBaseUrl);
}
