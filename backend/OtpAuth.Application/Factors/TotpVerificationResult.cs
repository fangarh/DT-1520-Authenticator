namespace OtpAuth.Application.Factors;

public sealed record TotpVerificationResult
{
    public required TotpVerificationStatus Status { get; init; }

    public Guid? EnrollmentId { get; init; }

    public long? TimeStep { get; init; }

    public static TotpVerificationResult Valid(Guid enrollmentId, long timeStep) => new()
    {
        Status = TotpVerificationStatus.Valid,
        EnrollmentId = enrollmentId,
        TimeStep = timeStep,
    };

    public static TotpVerificationResult InvalidCode() => new()
    {
        Status = TotpVerificationStatus.InvalidCode,
    };

    public static TotpVerificationResult ReplayDetected(Guid enrollmentId, long timeStep) => new()
    {
        Status = TotpVerificationStatus.ReplayDetected,
        EnrollmentId = enrollmentId,
        TimeStep = timeStep,
    };
}

public enum TotpVerificationStatus
{
    InvalidCode = 0,
    Valid = 1,
    ReplayDetected = 2,
}
