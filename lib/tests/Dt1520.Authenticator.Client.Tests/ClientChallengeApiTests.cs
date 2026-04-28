using System.Net;
using System.Text.Json;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class ClientChallengeApiTests
{
    private static readonly Guid TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    private static readonly Guid ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    private static readonly Guid ChallengeId = Guid.Parse("52c7b0a4-bc5a-4a3c-86ec-1dd260f264d5");
    private static readonly Guid TargetDeviceId = Guid.Parse("2fb5e7a6-d0c1-4f5f-8e87-85051fd03c13");

    [Fact]
    public async Task CreateChallengeAsyncSendsContractJsonAndIdempotencyHeader()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json(ChallengeJson("pending", "push")));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.CreateChallengeAsync(new CreateChallengeRequest
        {
            TenantId = TenantId,
            ApplicationClientId = ApplicationClientId,
            Subject = new ChallengeSubject
            {
                ExternalUserId = "user-123",
                Username = "ivan.petrov",
            },
            Operation = new ChallengeOperation
            {
                Type = ChallengeOperationType.StepUp,
                DisplayName = "Update VCS credentials",
            },
            PreferredFactors = [ChallengeFactorType.Push, ChallengeFactorType.Totp],
            TargetDeviceId = TargetDeviceId,
            Callback = new ChallengeCallbackRegistration
            {
                Url = new Uri("https://integrator.test/callbacks/dt1520"),
            },
            CorrelationId = "auth-req-001",
            IdempotencyKey = "idem-001",
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(ChallengeStatus.Pending, result.Value?.Status);
        Assert.Equal(ChallengeFactorType.Push, result.Value?.FactorType);

        Assert.Equal(2, handler.Requests.Count);
        var request = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://auth.test/api/v1/challenges", request.Uri);
        Assert.Equal("Bearer token-one", request.Authorization);
        Assert.Equal("idem-001", Assert.Single(request.Headers["Idempotency-Key"]));
        Assert.DoesNotContain("idempotencyKey", request.Body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(request.Body);
        var root = document.RootElement;
        Assert.Equal(TenantId, root.GetProperty("tenantId").GetGuid());
        Assert.Equal(ApplicationClientId, root.GetProperty("applicationClientId").GetGuid());
        Assert.Equal("user-123", root.GetProperty("subject").GetProperty("externalUserId").GetString());
        Assert.Equal("step_up", root.GetProperty("operation").GetProperty("type").GetString());
        Assert.Equal("push", root.GetProperty("preferredFactors")[0].GetString());
        Assert.Equal("totp", root.GetProperty("preferredFactors")[1].GetString());
        Assert.Equal(TargetDeviceId, root.GetProperty("targetDeviceId").GetGuid());
        Assert.Equal("https://integrator.test/callbacks/dt1520", root.GetProperty("callback").GetProperty("url").GetString());
        Assert.Equal("auth-req-001", root.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task GetChallengeAsyncReadsChallengeResponse()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json(ChallengeJson("approved", "totp", approvedAt: "2026-04-27T10:02:00Z")));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.GetChallengeAsync(ChallengeId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChallengeId, result.Value?.Id);
        Assert.Equal(ChallengeStatus.Approved, result.Value?.Status);
        Assert.Equal(ChallengeFactorType.Totp, result.Value?.FactorType);
        Assert.Equal("https://auth.test/api/v1/challenges/52c7b0a4-bc5a-4a3c-86ec-1dd260f264d5", handler.Requests[1].Uri);
    }

    [Fact]
    public async Task VerifyTotpAsyncSendsOnlyCodeAndRedactsToString()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json(ChallengeJson("approved", "totp")));
        using var client = ClientTestFactory.Create(handler);
        var verifyRequest = new VerifyTotpRequest { Code = "123456" };

        var result = await client.VerifyTotpAsync(ChallengeId, verifyRequest);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChallengeStatus.Approved, result.Value?.Status);
        Assert.Equal("https://auth.test/api/v1/challenges/52c7b0a4-bc5a-4a3c-86ec-1dd260f264d5/verify-totp", handler.Requests[1].Uri);
        Assert.Equal("""{"code":"123456"}""", handler.Requests[1].Body);
        Assert.DoesNotContain("123456", verifyRequest.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ChallengeRequestTypesRedactSensitiveDisplayValues()
    {
        var subject = new ChallengeSubject
        {
            ExternalUserId = "user-123",
            Username = "ivan.petrov",
        };
        var callback = new ChallengeCallbackRegistration
        {
            Url = new Uri("https://integrator.test/callbacks/dt1520?secret=callback-secret"),
        };
        var request = new CreateChallengeRequest
        {
            TenantId = TenantId,
            ApplicationClientId = ApplicationClientId,
            Subject = subject,
            Operation = new ChallengeOperation { Type = ChallengeOperationType.Login },
            Callback = callback,
        };

        Assert.DoesNotContain("user-123", subject.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ivan.petrov", subject.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("callback-secret", callback.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("integrator.test", callback.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("user-123", request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("integrator.test", request.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public async Task VerifyTotpAsyncRejectsInvalidCodesBeforeNetwork(string code)
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.Token("token-one", 3600));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.VerifyTotpAsync(ChallengeId, new VerifyTotpRequest { Code = code });

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.ValidationFailed, result.Error?.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateChallengeAsyncRejectsInvalidCallbackBeforeNetwork()
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.Token("token-one", 3600));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.CreateChallengeAsync(new CreateChallengeRequest
        {
            TenantId = TenantId,
            ApplicationClientId = ApplicationClientId,
            Subject = new ChallengeSubject { ExternalUserId = "user-123" },
            Operation = new ChallengeOperation { Type = ChallengeOperationType.Login },
            Callback = new ChallengeCallbackRegistration { Url = new Uri("ftp://integrator.test/callback") },
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.ValidationFailed, result.Error?.Kind);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, Dt1520AuthenticatorErrorKind.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity, Dt1520AuthenticatorErrorKind.ValidationFailed)]
    public async Task ChallengeApisMapExpectedProblemDetails(HttpStatusCode statusCode, Dt1520AuthenticatorErrorKind expectedKind)
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Problem(statusCode));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.VerifyTotpAsync(ChallengeId, new VerifyTotpRequest { Code = "123456" });

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedKind, result.Error?.Kind);
        Assert.Equal((int)statusCode, result.Error?.StatusCode);
    }

    private static string ChallengeJson(string status, string factorType, string? approvedAt = null)
    {
        var approvedAtJson = approvedAt is null ? string.Empty : $",\"approvedAt\":\"{approvedAt}\"";
        return $$"""
            {
              "id": "{{ChallengeId}}",
              "tenantId": "{{TenantId}}",
              "applicationClientId": "{{ApplicationClientId}}",
              "factorType": "{{factorType}}",
              "status": "{{status}}",
              "expiresAt": "2026-04-27T10:05:00Z",
              "targetDeviceId": "{{TargetDeviceId}}",
              "correlationId": "auth-req-001"{{approvedAtJson}}
            }
            """;
    }
}
