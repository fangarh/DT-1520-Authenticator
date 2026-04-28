namespace Dt1520.Authenticator.ReferenceDesktop;

public sealed record StartProtectedOperationRequest(
    string ExternalUserId,
    string DisplayName);

public sealed record VerifyTotpFallbackRequest(string Code);
