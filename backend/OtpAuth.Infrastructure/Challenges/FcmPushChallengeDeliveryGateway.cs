using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class FcmPushChallengeDeliveryGateway : IPushChallengeDeliveryProviderGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IFcmAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<FcmPushChallengeDeliveryGateway> _logger;
    private readonly FcmPushChallengeDeliveryGatewayOptions _options;

    public FcmPushChallengeDeliveryGateway(
        HttpClient httpClient,
        IFcmAccessTokenProvider accessTokenProvider,
        PushChallengeDeliveryGatewayOptions options,
        ILogger<FcmPushChallengeDeliveryGateway> logger)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
        _options = options.Fcm;
    }

    public string ProviderName => PushChallengeDeliveryProviderNames.Fcm;

    public async Task<PushChallengeDispatchResult> DeliverAsync(
        PushChallengeDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.PushToken))
        {
            return PushChallengeDispatchResult.Failure("push_token_missing", isRetryable: false);
        }

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellationTokenSource.CancelAfter(_options.GetTimeout());

        var linkedToken = linkedCancellationTokenSource.Token;
        string accessToken;
        try
        {
            accessToken = await _accessTokenProvider.GetAccessTokenAsync(linkedToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "FCM access token acquisition timed out. DeliveryId={DeliveryId} ChallengeId={ChallengeId}",
                request.DeliveryId,
                request.ChallengeId);

            return PushChallengeDispatchResult.Failure("push_provider_timeout", isRetryable: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "FCM access token acquisition failed. DeliveryId={DeliveryId} ChallengeId={ChallengeId} ExceptionType={ExceptionType}",
                request.DeliveryId,
                request.ChallengeId,
                exception.GetType().Name);

            return PushChallengeDispatchResult.Failure("push_provider_auth_failed", isRetryable: true);
        }

        var endpoint = $"https://fcm.googleapis.com/v1/projects/{_options.GetProjectId()}/messages:send";
        var payloadJson = JsonSerializer.Serialize(CreatePayload(request), SerializerOptions);
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.TryAddWithoutValidation("User-Agent", _options.GetUserAgent());

        try
        {
            using var response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                linkedToken);
            var responseBody = await response.Content.ReadAsStringAsync(linkedToken);
            if (response.IsSuccessStatusCode)
            {
                return PushChallengeDispatchResult.Success(ParseProviderMessageId(responseBody));
            }

            var dispatchResult = MapFailure(response.StatusCode, responseBody);
            _logger.LogWarning(
                "FCM push delivery rejected. DeliveryId={DeliveryId} ChallengeId={ChallengeId} DeviceId={DeviceId} StatusCode={StatusCode} ErrorCode={ErrorCode}",
                request.DeliveryId,
                request.ChallengeId,
                request.TargetDeviceId,
                (int)response.StatusCode,
                dispatchResult.ErrorCode ?? "unknown");

            return dispatchResult;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "FCM push delivery timed out. DeliveryId={DeliveryId} ChallengeId={ChallengeId} DeviceId={DeviceId}",
                request.DeliveryId,
                request.ChallengeId,
                request.TargetDeviceId);

            return PushChallengeDispatchResult.Failure("push_provider_timeout", isRetryable: true);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                "FCM push delivery transport error. DeliveryId={DeliveryId} ChallengeId={ChallengeId} DeviceId={DeviceId} ExceptionType={ExceptionType}",
                request.DeliveryId,
                request.ChallengeId,
                request.TargetDeviceId,
                exception.GetType().Name);

            return PushChallengeDispatchResult.Failure("push_provider_transport_error", isRetryable: true);
        }
    }

    private static object CreatePayload(PushChallengeDispatchRequest request)
    {
        return new
        {
            message = new
            {
                token = request.PushToken,
                notification = new
                {
                    title = "Authentication request",
                    body = CreateBody(request),
                },
                data = new Dictionary<string, string>
                {
                    ["challengeId"] = request.ChallengeId.ToString("D"),
                    ["operationType"] = request.OperationType,
                },
                android = new
                {
                    priority = "high",
                }
            }
        };
    }

    private static string CreateBody(PushChallengeDispatchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.OperationDisplayName))
        {
            return request.OperationDisplayName.Trim();
        }

        return $"Approve {request.OperationType.ToLowerInvariant()} request";
    }

    private static string? ParseProviderMessageId(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FcmSendResponse>(responseBody, SerializerOptions)?.Name;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PushChallengeDispatchResult MapFailure(HttpStatusCode statusCode, string responseBody)
    {
        var fcmStatus = ParseFcmStatus(responseBody);
        return fcmStatus switch
        {
            "UNREGISTERED" => PushChallengeDispatchResult.Failure("push_token_unregistered", isRetryable: false),
            "QUOTA_EXCEEDED" => PushChallengeDispatchResult.Failure("push_provider_rate_limited", isRetryable: true),
            "UNAVAILABLE" or "INTERNAL" => PushChallengeDispatchResult.Failure("push_provider_unavailable", isRetryable: true),
            "SENDER_ID_MISMATCH" or "THIRD_PARTY_AUTH_ERROR" or "PERMISSION_DENIED" => PushChallengeDispatchResult.Failure("push_provider_unauthorized", isRetryable: false),
            _ when statusCode == HttpStatusCode.TooManyRequests => PushChallengeDispatchResult.Failure("push_provider_rate_limited", isRetryable: true),
            _ when statusCode == HttpStatusCode.RequestTimeout || (int)statusCode >= 500 => PushChallengeDispatchResult.Failure("push_provider_unavailable", isRetryable: true),
            _ when statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden => PushChallengeDispatchResult.Failure("push_provider_unauthorized", isRetryable: false),
            _ => PushChallengeDispatchResult.Failure("push_provider_rejected", isRetryable: false),
        };
    }

    private static string? ParseFcmStatus(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            var response = JsonSerializer.Deserialize<FcmErrorResponse>(responseBody, SerializerOptions);
            return response?.Error?.Details?
                       .FirstOrDefault(detail => string.Equals(
                           detail.Type,
                           "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                           StringComparison.Ordinal))
                       ?.ErrorCode
                   ?? response?.Error?.Status;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record FcmSendResponse(string? Name);

    private sealed record FcmErrorResponse(FcmErrorEnvelope? Error);

    private sealed record FcmErrorEnvelope(string? Status, IReadOnlyCollection<FcmErrorDetail>? Details);

    private sealed record FcmErrorDetail(
        [property: JsonPropertyName("@type")] string? Type,
        string? ErrorCode);
}
