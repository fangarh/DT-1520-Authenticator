using OtpAuth.Application.Factors;
using Riok.Mapperly.Abstractions;

namespace OtpAuth.Infrastructure.Factors;

internal sealed record TotpEnrollmentPersistenceModel
{
    public required Guid EnrollmentId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required byte[] SecretCiphertext { get; init; }

    public required byte[] SecretNonce { get; init; }

    public required byte[] SecretTag { get; init; }

    public required int KeyVersion { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }
}

internal sealed record TotpEnrollmentMaterial
{
    public required Guid EnrollmentId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required int KeyVersion { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }
}

[Mapper]
internal static partial class TotpEnrollmentDataMapper
{
    [MapperIgnoreSource(nameof(TotpEnrollmentPersistenceModel.SecretCiphertext))]
    [MapperIgnoreSource(nameof(TotpEnrollmentPersistenceModel.SecretNonce))]
    [MapperIgnoreSource(nameof(TotpEnrollmentPersistenceModel.SecretTag))]
    public static partial TotpEnrollmentMaterial ToMaterial(TotpEnrollmentPersistenceModel source);
}
