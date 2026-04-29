namespace OtpAuth.Api.Admin;

public sealed record AdminCreateCombinedOnboardingPackageHttpRequest
{
    public Guid TenantId { get; init; }

    public Guid ApplicationClientId { get; init; }

    public string? ExternalUserId { get; init; }

    public string? Platform { get; init; }

    public int? TtlMinutes { get; init; }

    public string? Issuer { get; init; }

    public string? Label { get; init; }
}

public sealed record AdminCreateCombinedOnboardingPackageHttpResponse
{
    public required AdminDeviceOnboardingArtifactHttpResponse DeviceArtifact { get; init; }

    public required string ActivationPayload { get; init; }

    public required AdminTotpEnrollmentCommandHttpResponse TotpEnrollment { get; init; }
}
