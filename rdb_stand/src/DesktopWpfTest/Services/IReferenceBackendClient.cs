namespace Dt1520.Authenticator.DesktopWpfTest.Services;

public interface IReferenceBackendClient
{
    Task<ReferenceBackendResult<ReferenceApprovalSession>> StartOperationAsync(
        string externalUserId,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<ReferenceBackendResult<ReferenceApprovalSession>> GetStatusAsync(
        string pollingPath,
        CancellationToken cancellationToken = default);

    Task<ReferenceBackendResult<ReferenceApprovalSession>> SubmitTotpAsync(
        string sessionId,
        string code,
        CancellationToken cancellationToken = default);
}
