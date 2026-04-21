using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Infrastructure.Webhooks;

public sealed class HttpWebhookEventDeliveryGateway : IWebhookEventDeliveryGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWebhookEventDeliveryGateway> _logger;
    private readonly WebhookDeliveryGatewayOptions _options;

    public HttpWebhookEventDeliveryGateway(
        HttpClient httpClient,
        WebhookDeliveryGatewayOptions options,
        ILogger<HttpWebhookEventDeliveryGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options;
    }

    public async Task<WebhookEventDispatchResult> DeliverAsync(
        WebhookEventDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var payloadBytes = Encoding.UTF8.GetBytes(request.PayloadJson);
        var signature = CreateSignature(payloadBytes);

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellationTokenSource.CancelAfter(_options.GetTimeout());

        using var message = new HttpRequestMessage(HttpMethod.Post, request.EndpointUrl)
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
                return WebhookEventDispatchResult.Success();
            }

            var errorCode = MapErrorCode(response.StatusCode);
            _logger.LogWarning(
                "Webhook delivery rejected. DeliveryId={DeliveryId} EventId={EventId} EventType={EventType} EndpointHost={EndpointHost} StatusCode={StatusCode}",
                request.DeliveryId,
                request.EventId,
                request.EventType,
                request.EndpointUrl.Host,
                (int)response.StatusCode);

            return WebhookEventDispatchResult.Failure(
                errorCode,
                IsRetryable(response.StatusCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Webhook delivery timed out. DeliveryId={DeliveryId} EventId={EventId} EventType={EventType} EndpointHost={EndpointHost}",
                request.DeliveryId,
                request.EventId,
                request.EventType,
                request.EndpointUrl.Host);

            return WebhookEventDispatchResult.Failure("webhook_timeout", isRetryable: true);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Webhook delivery transport error. DeliveryId={DeliveryId} EventId={EventId} EventType={EventType} EndpointHost={EndpointHost}",
                request.DeliveryId,
                request.EventId,
                request.EventType,
                request.EndpointUrl.Host);

            return WebhookEventDispatchResult.Failure("webhook_transport_error", isRetryable: true);
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
            HttpStatusCode.BadRequest => "webhook_rejected_bad_request",
            HttpStatusCode.Unauthorized => "webhook_rejected_unauthorized",
            HttpStatusCode.Forbidden => "webhook_rejected_forbidden",
            HttpStatusCode.NotFound => "webhook_rejected_not_found",
            HttpStatusCode.TooManyRequests => "webhook_rate_limited",
            _ when (int)statusCode >= 500 => "webhook_server_error",
            _ => "webhook_rejected",
        };
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }
}
