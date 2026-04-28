namespace Dt1520.Authenticator.Desktop;

/// <summary>
/// Desktop-safe approval session returned by an integrator backend.
/// </summary>
public sealed record DesktopApprovalSession
{
    /// <summary>
    /// Opaque session identifier from the integrator backend.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Relative backend path used to read the latest approval status.
    /// </summary>
    public required string PollingPath { get; init; }

    /// <summary>
    /// Current approval status.
    /// </summary>
    public required DesktopApprovalSessionStatus Status { get; init; }

    /// <summary>
    /// Optional backend-provided expiry timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Optional safe display message for the desktop UI.
    /// </summary>
    public string? DisplayMessage { get; init; }

    /// <summary>
    /// Optional safe failure code or reason from the integrator backend.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Indicates whether this state no longer needs polling.
    /// </summary>
    public bool IsTerminal => IsTerminalStatus(Status);

    /// <summary>
    /// Returns <c>true</c> when the status no longer needs polling.
    /// </summary>
    public static bool IsTerminalStatus(DesktopApprovalSessionStatus status)
    {
        return status is DesktopApprovalSessionStatus.Approved
            or DesktopApprovalSessionStatus.Denied
            or DesktopApprovalSessionStatus.Expired
            or DesktopApprovalSessionStatus.Failed
            or DesktopApprovalSessionStatus.Cancelled;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(DesktopApprovalSession)} {{ {nameof(Status)} = {Status}, {nameof(IsTerminal)} = {IsTerminal} }}";
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            throw new ArgumentException("Approval session id is required.", nameof(SessionId));
        }

        if (string.IsNullOrWhiteSpace(PollingPath))
        {
            throw new ArgumentException("Approval polling path is required.", nameof(PollingPath));
        }

        if (Uri.TryCreate(PollingPath, UriKind.Absolute, out _)
            || PollingPath.StartsWith("//", StringComparison.Ordinal)
            || !PollingPath.StartsWith("/", StringComparison.Ordinal)
            || PollingPath.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Approval polling path must be a safe relative path under the integrator backend.",
                nameof(PollingPath));
        }
    }
}
