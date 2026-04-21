using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OtpAuth.Application.Challenges;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class FcmPushChallengeDeliveryGatewayTests
{
    [Fact]
    public async Task DeliverAsync_SendsBearerAuthorizedSanitizedPayload()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "name": "projects/test-project/messages/0:1234567890"
            }
            """);
        var gateway = CreateGateway(handler);

        var result = await gateway.DeliverAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("projects/test-project/messages/0:1234567890", result.ProviderMessageId);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "https://fcm.googleapis.com/v1/projects/test-project/messages:send",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer test-access-token", handler.LastAuthorization);
        Assert.Equal("OtpAuth-Push-FCM-Test/1.0", handler.LastUserAgent);
        Assert.Contains("\"challengeId\":\"73d28d84-34b4-4cf5-ae3f-dfbdc46b9910\"", handler.LastBody);
        Assert.Contains("\"operationType\":\"Login\"", handler.LastBody);
        Assert.DoesNotContain("user-push", handler.LastBody);
        Assert.DoesNotContain("corr-001", handler.LastBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "UNREGISTERED", "push_token_unregistered", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "QUOTA_EXCEEDED", "push_provider_rate_limited", true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE", "push_provider_unavailable", true)]
    [InlineData(HttpStatusCode.Forbidden, "SENDER_ID_MISMATCH", "push_provider_unauthorized", false)]
    public async Task DeliverAsync_MapsFcmErrorsToRetryPolicy(
        HttpStatusCode statusCode,
        string fcmStatus,
        string errorCode,
        bool isRetryable)
    {
        var handler = new RecordingHttpMessageHandler(
            statusCode,
            $$"""
            {
              "error": {
                "status": "{{fcmStatus}}",
                "details": [
                  {
                    "@type": "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                    "errorCode": "{{fcmStatus}}"
                  }
                ]
              }
            }
            """);
        var gateway = CreateGateway(handler);

        var result = await gateway.DeliverAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(isRetryable, result.IsRetryable);
    }

    private static FcmPushChallengeDeliveryGateway CreateGateway(HttpMessageHandler handler)
    {
        return new FcmPushChallengeDeliveryGateway(
            new HttpClient(handler),
            new StubAccessTokenProvider(),
            new PushChallengeDeliveryGatewayOptions
            {
                Fcm = new FcmPushChallengeDeliveryGatewayOptions
                {
                    ProjectId = "test-project",
                    ServiceAccountJsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                        """
                        {
                          "type": "service_account",
                          "project_id": "test-project",
                          "client_email": "svc@test-project.iam.gserviceaccount.com",
                          "private_key": "-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----\n"
                        }
                        """)),
                    TimeoutSeconds = 5,
                    UserAgent = "OtpAuth-Push-FCM-Test/1.0",
                }
            },
            NullLogger<FcmPushChallengeDeliveryGateway>.Instance);
    }

    private static PushChallengeDispatchRequest CreateRequest()
    {
        return new PushChallengeDispatchRequest
        {
            DeliveryId = Guid.Parse("4b21f5ad-9408-4811-8914-65d5d37adf28"),
            ChallengeId = Guid.Parse("73d28d84-34b4-4cf5-ae3f-dfbdc46b9910"),
            TargetDeviceId = Guid.Parse("c1f9a9a1-9e27-4a0f-bf1b-6a8ddbd866e0"),
            PushToken = "push-token",
            ExternalUserId = "user-push",
            OperationType = "Login",
            OperationDisplayName = "Approve login",
            CorrelationId = "corr-001",
        };
    }

    private sealed class StubAccessTokenProvider : IFcmAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult("test-access-token");
        }
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastAuthorization { get; private set; }

        public string? LastUserAgent { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastUserAgent = request.Headers.UserAgent.ToString();
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
