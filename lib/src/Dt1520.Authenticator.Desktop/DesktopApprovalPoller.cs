using System.Net.Http.Headers;
using System.Text.Json;

namespace Dt1520.Authenticator.Desktop;

/// <summary>
/// Polls an integrator backend for desktop approval completion.
/// </summary>
public sealed class DesktopApprovalPoller
{
    private static readonly MediaTypeWithQualityHeaderValue JsonAcceptHeader = new("application/json");
    private readonly HttpClient _httpClient;
    private readonly DesktopApprovalPollingOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a desktop approval poller.
    /// </summary>
    public DesktopApprovalPoller(
        HttpClient httpClient,
        DesktopApprovalPollingOptions options,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Polls the integrator backend until a terminal approval outcome, caller cancellation or local timeout.
    /// </summary>
    public async Task<DesktopApprovalOutcome> PollUntilCompletedAsync(
        DesktopApprovalSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();

        var current = ExpireIfNeeded(session, _timeProvider.GetUtcNow());
        if (current.IsTerminal)
        {
            return DesktopApprovalOutcome.FromSession(current);
        }

        var deadline = _timeProvider.GetUtcNow().Add(_options.Timeout);

        while (true)
        {
            var now = _timeProvider.GetUtcNow();
            current = ExpireIfNeeded(current, now);
            if (current.IsTerminal)
            {
                return DesktopApprovalOutcome.FromSession(current);
            }

            if (now >= deadline)
            {
                return DesktopApprovalOutcome.TimedOut(current);
            }

            try
            {
                var pollResult = await PollOnceAsync(current, cancellationToken).ConfigureAwait(false);
                if (pollResult.Outcome is not null)
                {
                    return pollResult.Outcome;
                }

                current = pollResult.Session;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return DesktopApprovalOutcome.Cancelled(current);
            }
            catch (HttpRequestException)
            {
                return DesktopApprovalOutcome.Failed(current, "Approval status polling transport failed.");
            }
            catch (JsonException)
            {
                return DesktopApprovalOutcome.Failed(current, "Approval status response JSON was invalid.");
            }

            if (current.IsTerminal)
            {
                return DesktopApprovalOutcome.FromSession(current);
            }

            var delay = GetNextDelay(current, deadline);
            try
            {
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return DesktopApprovalOutcome.Cancelled(current);
            }
        }
    }

    private async Task<(DesktopApprovalSession Session, DesktopApprovalOutcome? Outcome)> PollOnceAsync(
        DesktopApprovalSession current,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildStatusUri(current));
        request.Headers.Accept.Add(JsonAcceptHeader);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return (current, DesktopApprovalOutcome.Failed(
                    current,
                    "Integrator backend rejected approval status polling.",
                    (int)response.StatusCode));
        }

        var session = await ReadSessionAsync(response, current, cancellationToken).ConfigureAwait(false);
        return (session, null);
    }

    private Uri BuildStatusUri(DesktopApprovalSession session)
    {
        session.Validate();

        var uri = new Uri(_options.BackendBaseUrl, session.PollingPath);
        if (!SameOrigin(_options.BackendBaseUrl, uri))
        {
            throw new ArgumentException("Approval polling path must stay under the configured integrator backend.", nameof(session));
        }

        return uri;
    }

    private async Task<DesktopApprovalSession> ReadSessionAsync(
        HttpResponseMessage response,
        DesktopApprovalSession previous,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > _options.MaxStatusResponseBytes)
        {
            throw new JsonException("Approval status response exceeded the configured size limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (memory.Length + read > _options.MaxStatusResponseBytes)
            {
                throw new JsonException("Approval status response exceeded the configured size limit.");
            }

            memory.Write(buffer, 0, read);
        }

        memory.Position = 0;
        using var document = await JsonDocument.ParseAsync(memory, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Approval status response must be a JSON object.");
        }

        var status = ReadStatus(root);
        var pollingPath = ReadString(root, "pollingPath", "statusPath") ?? previous.PollingPath;
        var session = new DesktopApprovalSession
        {
            SessionId = ReadString(root, "sessionId", "id") ?? previous.SessionId,
            PollingPath = pollingPath,
            Status = status,
            ExpiresAt = ReadDateTimeOffset(root, "expiresAt") ?? previous.ExpiresAt,
            DisplayMessage = ReadString(root, "displayMessage", "message"),
            FailureReason = ReadString(root, "failureReason", "reason"),
        };
        session.Validate();

        return ExpireIfNeeded(session, _timeProvider.GetUtcNow());
    }

    private TimeSpan GetNextDelay(DesktopApprovalSession session, DateTimeOffset deadline)
    {
        var now = _timeProvider.GetUtcNow();
        var delay = _options.PollInterval;
        var untilDeadline = deadline - now;
        if (untilDeadline > TimeSpan.Zero && untilDeadline < delay)
        {
            delay = untilDeadline;
        }

        if (session.ExpiresAt is not null)
        {
            var untilExpiry = session.ExpiresAt.Value - now;
            if (untilExpiry > TimeSpan.Zero && untilExpiry < delay)
            {
                delay = untilExpiry;
            }
        }

        return delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : delay;
    }

    private static DesktopApprovalSession ExpireIfNeeded(
        DesktopApprovalSession session,
        DateTimeOffset utcNow)
    {
        if (session.Status == DesktopApprovalSessionStatus.Waiting
            && session.ExpiresAt is not null
            && session.ExpiresAt <= utcNow)
        {
            return session with { Status = DesktopApprovalSessionStatus.Expired };
        }

        return session;
    }

    private static DesktopApprovalSessionStatus ReadStatus(JsonElement root)
    {
        var statusValue = ReadString(root, "status")
            ?? throw new JsonException("Approval status response must contain status.");

        if (!TryParseStatus(statusValue, out var status))
        {
            throw new JsonException("Approval status response contained an unsupported status.");
        }

        return status;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.TryGetDateTimeOffset(out var value) ? value : null;
    }

    private static bool TryParseStatus(string value, out DesktopApprovalSessionStatus status)
    {
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized switch
        {
            "waiting" or "pending" => Set(out status, DesktopApprovalSessionStatus.Waiting),
            "approved" => Set(out status, DesktopApprovalSessionStatus.Approved),
            "denied" => Set(out status, DesktopApprovalSessionStatus.Denied),
            "expired" => Set(out status, DesktopApprovalSessionStatus.Expired),
            "failed" => Set(out status, DesktopApprovalSessionStatus.Failed),
            "cancelled" or "canceled" => Set(out status, DesktopApprovalSessionStatus.Cancelled),
            _ => Fail(out status),
        };
    }

    private static bool SameOrigin(Uri left, Uri right)
    {
        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port;
    }

    private static bool Set(out DesktopApprovalSessionStatus target, DesktopApprovalSessionStatus value)
    {
        target = value;
        return true;
    }

    private static bool Fail(out DesktopApprovalSessionStatus target)
    {
        target = default;
        return false;
    }
}
