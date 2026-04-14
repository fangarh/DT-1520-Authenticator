namespace OtpAuth.Application.Integrations;

public sealed record IntegrationAccessTokenIntrospectionResult
{
    public required bool IsRecognizedToken { get; init; }

    public required bool IsActive { get; init; }

    public string? ClientId { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public string? Scope { get; init; }

    public string? JwtId { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public static IntegrationAccessTokenIntrospectionResult Unrecognized() => new()
    {
        IsRecognizedToken = false,
        IsActive = false,
    };
}
