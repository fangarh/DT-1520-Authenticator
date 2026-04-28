using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dt1520.Authenticator.DesktopWpfTest.Services;

public sealed class ReferenceBackendClient : IReferenceBackendClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient _httpClient;

    public ReferenceBackendClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ReferenceBackendResult<ReferenceApprovalSession>> StartOperationAsync(
        string externalUserId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var request = new StartProtectedOperationRequest(externalUserId, displayName);
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/reference/operations",
            request,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return await ReadSessionResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReferenceBackendResult<ReferenceApprovalSession>> GetStatusAsync(
        string pollingPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeRelativePath(pollingPath))
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure("Polling path is not a safe backend-relative path.");
        }

        using var response = await _httpClient.GetAsync(pollingPath, cancellationToken).ConfigureAwait(false);
        return await ReadSessionResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ReferenceBackendResult<ReferenceApprovalSession>> SubmitTotpAsync(
        string sessionId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/reference/operations/{Uri.EscapeDataString(sessionId)}/totp";
        using var response = await _httpClient.PostAsJsonAsync(
            path,
            new VerifyTotpFallbackRequest(code),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return await ReadSessionResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ReferenceBackendResult<ReferenceApprovalSession>> ReadSessionResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure(
                $"Reference backend returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        try
        {
            var session = await response.Content.ReadFromJsonAsync<ReferenceApprovalSession>(
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            return session is null
                ? ReferenceBackendResult<ReferenceApprovalSession>.Failure("Reference backend returned an empty session response.")
                : ReferenceBackendResult<ReferenceApprovalSession>.Success(session);
        }
        catch (JsonException)
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure("Reference backend returned invalid session JSON.");
        }
    }

    private static bool IsSafeRelativePath(string pollingPath)
    {
        return !string.IsNullOrWhiteSpace(pollingPath)
            && pollingPath.StartsWith("/", StringComparison.Ordinal)
            && !pollingPath.StartsWith("//", StringComparison.Ordinal)
            && !Uri.TryCreate(pollingPath, UriKind.Absolute, out _)
            && !pollingPath.Any(char.IsControl);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
