using System.Text.Json.Serialization;
using Dt1520.Authenticator.Desktop;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed record StartProtectedOperationRequest
{
    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public string DisplayName { get; init; } = "Reference protected operation";
}

public sealed record VerifyTotpFallbackRequest
{
    public required string Code { get; init; }
}

public sealed record ReferenceApprovalSession
{
    public required string SessionId { get; init; }

    public required string PollingPath { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required DesktopApprovalSessionStatus Status { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? DisplayMessage { get; init; }

    public string? FailureReason { get; init; }

    public bool IsCommitted { get; init; }

    public required ReferenceLatencyTimestamps Latency { get; init; }
}

public sealed record ReferenceLatencyTimestamps
{
    public DateTimeOffset? DesktopSubmittedAtUtc { get; init; }

    public DateTimeOffset? BackendChallengeRequestedAtUtc { get; init; }

    public DateTimeOffset? ChallengeCreatedAtUtc { get; init; }

    public DateTimeOffset? TotpChallengeRequestedAtUtc { get; init; }

    public DateTimeOffset? TotpChallengeCreatedAtUtc { get; init; }

    public DateTimeOffset? CallbackReceivedAtUtc { get; init; }

    public DateTimeOffset? TotpSubmittedAtUtc { get; init; }

    public DateTimeOffset? TerminalAtUtc { get; init; }
}
