using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class HttpChallengeCallbackDeliveryGateway : IChallengeCallbackDeliveryGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpChallengeCallbackDeliveryGateway> _logger;
    private readonly ChallengeCallbackDeliveryGatewayOptions _options;

    public HttpChallengeCallbackDeliveryGateway(
        HttpClient httpClient,
        ChallengeCallbackDeliveryGatewayOptions options,
        ILogger<HttpChallengeCallbackDeliveryGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options;
    }

    public async Task<ChallengeCallbackDispatchResult> DeliverAsync(
        ChallengeCallbackDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = CreatePayload(request);
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var signature = CreateSignature(payloadBytes);

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellationTokenSource.CancelAfter(_options.GetTimeout());

        using var message = new HttpRequestMessage(HttpMethod.Post, request.CallbackUrl)
        {
            Content = new ByteArrayContent(payloadBytes),
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        message.Headers.TryAddWithoutValidation("X-OTPAuth-Signature", signature);
        message.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        try
        {
            using var response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellationTokenSource.Token);

            if (response.IsSuccessStatusCode)
            {
                return ChallengeCallbackDispatchResult.Success();
            }

            var errorCode = MapErrorCode(response.StatusCode);
            _logger.LogWarning(
                "Challenge callback delivery rejected. DeliveryId={DeliveryId} ChallengeId={ChallengeId} EventType={EventType} CallbackHost={CallbackHost} StatusCode={StatusCode}",
                request.DeliveryId,
                request.ChallengeId,
                ChallengeCallbackDeliveryDataMapper.ToPersistenceValue(request.EventType),
                request.CallbackUrl.Host,
                (int)response.StatusCode);

            return ChallengeCallbackDispatchResult.Failure(
                errorCode,
                IsRetryable(response.StatusCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Challenge callback delivery timed out. DeliveryId={DeliveryId} ChallengeId={ChallengeId} EventType={EventType} CallbackHost={CallbackHost}",
                request.DeliveryId,
                request.ChallengeId,
                ChallengeCallbackDeliveryDataMapper.ToPersistenceValue(request.EventType),
                request.CallbackUrl.Host);

            return ChallengeCallbackDispatchResult.Failure("callback_timeout", isRetryable: true);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Challenge callback delivery transport error. DeliveryId={DeliveryId} ChallengeId={ChallengeId} EventType={EventType} CallbackHost={CallbackHost}",
                request.DeliveryId,
                request.ChallengeId,
                ChallengeCallbackDeliveryDataMapper.ToPersistenceValue(request.EventType),
                request.CallbackUrl.Host);

            return ChallengeCallbackDispatchResult.Failure("callback_transport_error", isRetryable: true);
        }
    }

    private string CreateSignature(byte[] payloadBytes)
    {
        using var hmac = new HMACSHA256(_options.GetSigningKeyBytes());
        var signatureBytes = hmac.ComputeHash(payloadBytes);
        return $"sha256={Convert.ToHexString(signatureBytes).ToLowerInvariant()}";
    }

    private static string MapErrorCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "callback_rejected_bad_request",
            HttpStatusCode.Unauthorized => "callback_rejected_unauthorized",
            HttpStatusCode.Forbidden => "callback_rejected_forbidden",
            HttpStatusCode.NotFound => "callback_rejected_not_found",
            HttpStatusCode.TooManyRequests => "callback_rate_limited",
            _ when (int)statusCode >= 500 => "callback_server_error",
            _ => "callback_rejected",
        };
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static object CreatePayload(ChallengeCallbackDispatchRequest request)
    {
        return new
        {
            eventId = request.DeliveryId,
            eventType = ChallengeCallbackDeliveryDataMapper.ToPersistenceValue(request.EventType),
            occurredAt = request.OccurredAtUtc,
            challenge = new
            {
                id = request.Challenge.Id,
                tenantId = request.Challenge.TenantId,
                applicationClientId = request.Challenge.ApplicationClientId,
                factorType = request.Challenge.FactorType.ToString().ToLowerInvariant(),
                status = request.Challenge.Status.ToString().ToLowerInvariant(),
                expiresAt = request.Challenge.ExpiresAt,
                targetDeviceId = request.Challenge.TargetDeviceId,
                approvedAt = request.Challenge.ApprovedUtc,
                deniedAt = request.Challenge.DeniedUtc,
                correlationId = request.Challenge.CorrelationId,
            }
        };
    }
}
