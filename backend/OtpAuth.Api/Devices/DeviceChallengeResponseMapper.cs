using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Api.Devices;

public static class DeviceChallengeResponseMapper
{
    public static PendingDeviceChallengeHttpResponse MapPendingChallenge(Challenge challenge)
    {
        return new PendingDeviceChallengeHttpResponse
        {
            Id = challenge.Id,
            FactorType = ToContractValue(challenge.FactorType),
            Status = ToContractValue(challenge.Status),
            OperationType = ToContractValue(challenge.OperationType),
            OperationDisplayName = challenge.OperationDisplayName,
            Username = challenge.Username,
            ExpiresAt = challenge.ExpiresAt,
            CorrelationId = challenge.CorrelationId,
        };
    }

    private static string ToContractValue(FactorType factorType)
    {
        return factorType switch
        {
            FactorType.Totp => "totp",
            FactorType.Push => "push",
            FactorType.BackupCode => "backup_code",
            _ => "unknown",
        };
    }

    private static string ToContractValue(ChallengeStatus status)
    {
        return status switch
        {
            ChallengeStatus.Pending => "pending",
            ChallengeStatus.Approved => "approved",
            ChallengeStatus.Denied => "denied",
            ChallengeStatus.Expired => "expired",
            ChallengeStatus.Failed => "failed",
            _ => "unknown",
        };
    }

    private static string ToContractValue(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Login => "login",
            OperationType.StepUp => "step_up",
            OperationType.BackupCodeRecovery => "backup_code_recovery",
            OperationType.DeviceActivation => "device_activation",
            OperationType.TotpEnrollment => "totp_enrollment",
            _ => "unknown",
        };
    }
}
