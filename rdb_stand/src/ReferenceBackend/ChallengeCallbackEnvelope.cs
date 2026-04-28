using System.Text.Json.Serialization;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed record ChallengeCallbackEnvelope
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required ChallengeCallbackSnapshot Challenge { get; init; }
}

public sealed record ChallengeCallbackSnapshot
{
    public required Guid Id { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ChallengeStatus Status { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    public DateTimeOffset? DeniedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? CorrelationId { get; init; }
}
