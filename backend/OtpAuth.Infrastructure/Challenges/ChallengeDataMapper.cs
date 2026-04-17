using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using Riok.Mapperly.Abstractions;

namespace OtpAuth.Infrastructure.Challenges;

internal sealed record ChallengePersistenceModel
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required OperationType OperationType { get; init; }

    public string? OperationDisplayName { get; init; }

    public required FactorType FactorType { get; init; }

    public required ChallengeStatus Status { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public Guid? TargetDeviceId { get; init; }

    public DateTimeOffset? ApprovedUtc { get; init; }

    public DateTimeOffset? DeniedUtc { get; init; }

    public string? CorrelationId { get; init; }

    public string? CallbackUrl { get; init; }
}

[Mapper]
internal static partial class ChallengeDataMapper
{
    public static partial ChallengePersistenceModel ToPersistenceModel(Challenge source);

    public static partial Challenge ToDomainModel(ChallengePersistenceModel source);

    private static string? MapCallbackUrl(Uri? source)
    {
        return source?.ToString();
    }

    private static Uri? MapCallbackUrl(string? source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? null
            : new Uri(source, UriKind.Absolute);
    }
}
