using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class HttpChallengeCallbackDeliveryGatewayTests
{
    [Fact]
    public async Task DeliverAsync_SendsSignedJsonPayload()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK);
        var gateway = new HttpChallengeCallbackDeliveryGateway(
            new HttpClient(handler),
            new ChallengeCallbackDeliveryGatewayOptions
            {
                SigningKey = "test-signing-key",
                TimeoutSeconds = 5,
                UserAgent = "OtpAuth-Callbacks-Test/1.0",
            },
            NullLogger<HttpChallengeCallbackDeliveryGateway>.Instance);
        var request = CreateRequest();

        var result = await gateway.DeliverAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://crm.example.com/webhooks/otpauth", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(CreateSignature(handler.LastBody, "test-signing-key"), handler.LastSignature);
        Assert.Contains("\"eventType\":\"challenge.approved\"", handler.LastBody);
        Assert.Contains(request.Challenge.Id.ToString(), handler.LastBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true, "callback_rate_limited")]
    [InlineData(HttpStatusCode.BadRequest, false, "callback_rejected_bad_request")]
    public async Task DeliverAsync_MapsResponseCodesToRetryPolicy(HttpStatusCode statusCode, bool isRetryable, string errorCode)
    {
        var gateway = new HttpChallengeCallbackDeliveryGateway(
            new HttpClient(new RecordingHttpMessageHandler(statusCode)),
            new ChallengeCallbackDeliveryGatewayOptions
            {
                SigningKey = "test-signing-key",
                TimeoutSeconds = 5,
            },
            NullLogger<HttpChallengeCallbackDeliveryGateway>.Instance);

        var result = await gateway.DeliverAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(isRetryable, result.IsRetryable);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    private static ChallengeCallbackDispatchRequest CreateRequest()
    {
        var challenge = new Challenge
        {
            Id = Guid.Parse("73d28d84-34b4-4cf5-ae3f-dfbdc46b9910"),
            TenantId = Guid.Parse("9dc6562c-55bf-4cc0-8673-aaec996d7d7c"),
            ApplicationClientId = Guid.Parse("ed078497-7fb2-47e4-b89e-d2087d21b671"),
            ExternalUserId = "user-callback",
            Username = "user.callback",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve login",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Approved,
            ExpiresAt = DateTimeOffset.Parse("2026-04-20T13:05:00Z"),
            TargetDeviceId = Guid.Parse("c1f9a9a1-9e27-4a0f-bf1b-6a8ddbd866e0"),
            ApprovedUtc = DateTimeOffset.Parse("2026-04-20T13:00:00Z"),
            CorrelationId = "corr-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
        };

        return new ChallengeCallbackDispatchRequest
        {
            DeliveryId = Guid.Parse("4b21f5ad-9408-4811-8914-65d5d37adf28"),
            ChallengeId = challenge.Id,
            CallbackUrl = challenge.CallbackUrl!,
            EventType = ChallengeCallbackEventType.Approved,
            OccurredAtUtc = challenge.ApprovedUtc!.Value,
            Challenge = challenge,
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
