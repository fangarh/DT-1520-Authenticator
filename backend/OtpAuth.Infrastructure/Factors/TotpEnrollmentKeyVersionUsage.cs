namespace OtpAuth.Infrastructure.Factors;

public sealed record TotpEnrollmentKeyVersionUsage
{
    public required int KeyVersion { get; init; }

    public required int EnrollmentCount { get; init; }
}
