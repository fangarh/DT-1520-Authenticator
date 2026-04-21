using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OtpAuth.Application.Webhooks;
using OtpAuth.Infrastructure.Webhooks;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Webhooks;

public sealed class HttpWebhookEventDeliveryGatewayTests
{
    [Fact]
    public async Task DeliverAsync_SendsSignedJsonPayload()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK);
        var gateway = new HttpWebhookEventDeliveryGateway(
            new HttpClient(handler),
            new WebhookDeliveryGatewayOptions
            {
                SigningKey = "test-webhook-signing-key",
                TimeoutSeconds = 5,
                UserAgent = "OtpAuth-Webhooks-Test/1.0",
            },
            NullLogger<HttpWebhookEventDeliveryGateway>.Instance);
        var request = CreateRequest();

        var result = await gateway.DeliverAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://crm.example.com/webhooks/otpauth", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(CreateSignature(handler.LastBody, "test-webhook-signing-key"), handler.LastSignature);
        Assert.Contains("\"eventType\":\"challenge.approved\"", handler.LastBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true, "webhook_rate_limited")]
    [InlineData(HttpStatusCode.BadRequest, false, "webhook_rejected_bad_request")]
    public async Task DeliverAsync_MapsResponseCodesToRetryPolicy(HttpStatusCode statusCode, bool isRetryable, string errorCode)
    {
        var gateway = new HttpWebhookEventDeliveryGateway(
            new HttpClient(new RecordingHttpMessageHandler(statusCode)),
            new WebhookDeliveryGatewayOptions
            {
                SigningKey = "test-webhook-signing-key",
                TimeoutSeconds = 5,
            },
            NullLogger<HttpWebhookEventDeliveryGateway>.Instance);

        var result = await gateway.DeliverAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(isRetryable, result.IsRetryable);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    private static WebhookEventDispatchRequest CreateRequest()
    {
        return new WebhookEventDispatchRequest
        {
            DeliveryId = Guid.Parse("4b21f5ad-9408-4811-8914-65d5d37adf28"),
            EventId = Guid.Parse("73d28d84-34b4-4cf5-ae3f-dfbdc46b9910"),
            EventType = WebhookEventTypeNames.ChallengeApproved,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
            PayloadJson =
                """{"eventId":"73d28d84-34b4-4cf5-ae3f-dfbdc46b9910","eventType":"challenge.approved","occurredAt":"2026-04-20T13:00:00+00:00","challenge":{"id":"73d28d84-34b4-4cf5-ae3f-dfbdc46b9910"}}""",
        };
    }

    private static string CreateSignature(string body, string signingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return $"sha256={Convert.ToHexString(signatureBytes).ToLowerInvariant()}";
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastSignature { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastSignature = request.Headers.GetValues("X-OTPAuth-Signature").Single();
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain"),
            };
        }
    }
}
