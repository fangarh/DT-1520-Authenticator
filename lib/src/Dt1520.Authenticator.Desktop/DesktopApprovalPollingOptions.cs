namespace Dt1520.Authenticator.Desktop;

/// <summary>
/// Options for polling an integrator backend from a desktop application.
/// </summary>
public sealed record DesktopApprovalPollingOptions
{
    private static readonly TimeSpan MinimumPollInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaximumPollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinimumTimeout = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Integrator backend base URL. This is not the DT-1520 Authenticator base URL.
    /// </summary>
    public required Uri BackendBaseUrl { get; init; }

    /// <summary>
    /// Delay between waiting-state status reads.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum local desktop wait time before returning a timeout outcome.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum status response body size accepted from the integrator backend.
    /// </summary>
    public int MaxStatusResponseBytes { get; init; } = 16 * 1024;

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(DesktopApprovalPollingOptions)} {{ {nameof(PollInterval)} = {PollInterval}, {nameof(Timeout)} = {Timeout}, {nameof(MaxStatusResponseBytes)} = {MaxStatusResponseBytes} }}";
    }

    internal DesktopApprovalPollingOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(BackendBaseUrl);

        if (!BackendBaseUrl.IsAbsoluteUri
            || (BackendBaseUrl.Scheme != Uri.UriSchemeHttps && BackendBaseUrl.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Integrator backend base URL must be an absolute HTTP or HTTPS URL.", nameof(BackendBaseUrl));
        }

        if (!string.IsNullOrEmpty(BackendBaseUrl.UserInfo))
        {
            throw new ArgumentException("Integrator backend base URL must not contain user info or credentials.", nameof(BackendBaseUrl));
        }

        if (PollInterval < MinimumPollInterval || PollInterval > MaximumPollInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PollInterval),
                PollInterval,
                "Approval polling interval must be between 1 millisecond and 1 minute.");
        }

        if (Timeout < MinimumTimeout || Timeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Timeout),
                Timeout,
                "Approval polling timeout must be between 10 milliseconds and 30 minutes.");
        }

        if (MaxStatusResponseBytes is < 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxStatusResponseBytes),
                MaxStatusResponseBytes,
                "Approval status response limit must be between 1 KiB and 1 MiB.");
        }

        return this;
    }
}
