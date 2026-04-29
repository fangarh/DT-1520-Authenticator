using System.Security.Cryptography;
using System.Text;
using Dt1520.Authenticator.Client;
using Dt1520.Authenticator.Desktop;
using Dt1520.Authenticator.ReferenceBackend;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend.Tests;

public sealed class ProtectedOperationCoordinatorTests
{
    private static readonly Guid TenantId = Guid.Parse("310db3d2-3a5c-4057-b1b2-3caa2ddc204e");
    private static readonly Guid ApplicationClientId = Guid.Parse("398bc558-eeff-4719-9d19-b12d72e0f6fe");
    private static readonly Guid ChallengeId = Guid.Parse("8c9e00c5-0865-4b69-ae4e-e87fb5a80d6f");
    private static readonly Guid TotpChallengeId = Guid.Parse("7645bc25-01d2-42e6-9e2f-9f9a6722eb68");

    [Fact]
    public async Task StartAsyncCreatesDt1520ChallengeAndReturnsPollingSession()
    {
        var gateway = new FakeAuthenticatorGateway();
        var coordinator = CreateCoordinator(gateway);

        var result = await coordinator.StartAsync(new StartProtectedOperationRequest
        {
            ExternalUserId = " user-123 ",
            Username = "operator",
            DisplayName = "Reference update",
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DesktopApprovalSessionStatus.Waiting, result.Value?.Status);
        Assert.StartsWith("/api/reference/operations/", result.Value?.PollingPath, StringComparison.Ordinal);
        Assert.Equal("user-123", gateway.LastCreateOperation?.ExternalUserId);
        Assert.Equal(ChallengeId, gateway.PrimaryChallengeId);
        Assert.False(result.Value!.IsCommitted);
        Assert.NotNull(result.Value.Latency.BackendChallengeRequestedAtUtc);
    }

    [Fact]
    public async Task ApplyCallbackAsyncCommitsApprovedChallengeOnce()
    {
        var coordinator = CreateCoordinator(new FakeAuthenticatorGateway());
        var started = await StartAsync(coordinator);
        var request = CreateSignedCallbackRequest(started.SessionId, "approved");

        var first = await coordinator.ApplyCallbackAsync(request, CancellationToken.None);
        var second = await coordinator.ApplyCallbackAsync(
            CreateSignedCallbackRequest(started.SessionId, "denied"),
            CancellationToken.None);
        var current = await coordinator.GetSessionAsync(started.SessionId, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(DesktopApprovalSessionStatus.Approved, current?.Status);
        Assert.True(current?.IsCommitted);
        Assert.NotNull(current?.Latency.CallbackReceivedAtUtc);
        Assert.NotNull(current?.Latency.TerminalAtUtc);
    }

    [Fact]
    public async Task ApplyCallbackAsyncRejectsInvalidSignature()
    {
        var coordinator = CreateCoordinator(new FakeAuthenticatorGateway());
        var started = await StartAsync(coordinator);
        var request = CreateSignedCallbackRequest(started.SessionId, "approved");
        request.Headers["X-OTPAuth-Signature"] = "sha256=bad";

        var result = await coordinator.ApplyCallbackAsync(request, CancellationToken.None);
        var current = await coordinator.GetSessionAsync(started.SessionId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.Error?.StatusCode);
        Assert.Equal(DesktopApprovalSessionStatus.Waiting, current?.Status);
        Assert.False(current?.IsCommitted);
    }

    [Fact]
    public async Task VerifyTotpAsyncCreatesSeparateTotpChallengeAndDoesNotPersistCode()
    {
        var gateway = new FakeAuthenticatorGateway();
        var coordinator = CreateCoordinator(gateway);
        var started = await StartAsync(coordinator);

        var result = await coordinator.VerifyTotpAsync(
            started.SessionId,
            new VerifyTotpFallbackRequest { Code = "123456" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChallengeId, gateway.PrimaryChallengeId);
        Assert.Equal(TotpChallengeId, gateway.TotpFallbackChallengeId);
        Assert.Equal([ChallengeFactorType.Push, ChallengeFactorType.Totp], gateway.PrimaryPreferredFactors);
        Assert.Equal([ChallengeFactorType.Totp], gateway.TotpFallbackPreferredFactors);
        Assert.Equal(TotpChallengeId, gateway.LastVerifyChallengeId);
        Assert.Equal("123456", gateway.LastVerifyCode);
        Assert.Equal(DesktopApprovalSessionStatus.Approved, result.Value?.Status);
        Assert.True(result.Value?.IsCommitted);
        Assert.NotNull(result.Value?.Latency.TotpSubmittedAtUtc);
        Assert.DoesNotContain("123456", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task ApplyCallbackAsyncCannotOverwriteTotpFallbackCommit()
    {
        var gateway = new FakeAuthenticatorGateway();
        var coordinator = CreateCoordinator(gateway);
        var started = await StartAsync(coordinator);

        var fallback = await coordinator.VerifyTotpAsync(
            started.SessionId,
            new VerifyTotpFallbackRequest { Code = "123456" },
            CancellationToken.None);
        var deniedCallback = await coordinator.ApplyCallbackAsync(
            CreateSignedCallbackRequest(started.SessionId, "denied"),
            CancellationToken.None);
        var current = await coordinator.GetSessionAsync(started.SessionId, CancellationToken.None);

        Assert.True(fallback.IsSuccess);
        Assert.True(deniedCallback.IsSuccess);
        Assert.Equal(DesktopApprovalSessionStatus.Approved, current?.Status);
        Assert.True(current?.IsCommitted);
    }

    [Fact]
    public async Task ApplyCallbackAsyncKeepsFirstTerminalNonApprovedStatus()
    {
        var coordinator = CreateCoordinator(new FakeAuthenticatorGateway());
        var started = await StartAsync(coordinator);

        var denied = await coordinator.ApplyCallbackAsync(
            CreateSignedCallbackRequest(started.SessionId, "denied"),
            CancellationToken.None);
        var approved = await coordinator.ApplyCallbackAsync(
            CreateSignedCallbackRequest(started.SessionId, "approved"),
            CancellationToken.None);
        var current = await coordinator.GetSessionAsync(started.SessionId, CancellationToken.None);

        Assert.True(denied.IsSuccess);
        Assert.True(approved.IsSuccess);
        Assert.Equal(DesktopApprovalSessionStatus.Denied, current?.Status);
        Assert.False(current?.IsCommitted);
    }

    [Fact]
    public async Task VerifyTotpAsyncRejectsTerminalSessionWithoutCreatingFallbackChallenge()
    {
        var gateway = new FakeAuthenticatorGateway();
        var coordinator = CreateCoordinator(gateway);
        var started = await StartAsync(coordinator);
        await coordinator.ApplyCallbackAsync(
            CreateSignedCallbackRequest(started.SessionId, "denied"),
            CancellationToken.None);

        var result = await coordinator.VerifyTotpAsync(
            started.SessionId,
            new VerifyTotpFallbackRequest { Code = "123456" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.Error?.StatusCode);
        Assert.Null(gateway.TotpFallbackChallengeId);
        Assert.Null(gateway.LastVerifyChallengeId);
    }

    private static async Task<ReferenceApprovalSession> StartAsync(
        ProtectedOperationCoordinator coordinator)
    {
        var result = await coordinator.StartAsync(new StartProtectedOperationRequest
        {
            ExternalUserId = "user-123",
            DisplayName = "Reference update",
        }, CancellationToken.None);

        return result.Value!;
    }

    private static ProtectedOperationCoordinator CreateCoordinator(FakeAuthenticatorGateway gateway)
    {
        var backendOptions = Options.Create(new ReferenceBackendOptions
        {
            TenantId = TenantId,
            ApplicationClientId = ApplicationClientId,
            CallbackUrl = new Uri("https://reference.example.test/api/reference/callbacks/dt1520"),
        });
        var callbackOptions = new StaticOptionsMonitor<Dt1520.Authenticator.AspNetCore.Dt1520AuthenticatorAspNetCoreOptions>(
            new Dt1520.Authenticator.AspNetCore.Dt1520AuthenticatorAspNetCoreOptions
            {
                BaseUrl = new Uri("https://auth.example.test/"),
                ClientId = "client-one",
                ClientSecret = "secret-one",
                CallbackSigningSecret = "callback-secret",
            });

        return new ProtectedOperationCoordinator(
            gateway,
            new Dt1520.Authenticator.AspNetCore.Dt1520AuthenticatorCallbackValidator(
                callbackOptions,
                NullLogger<Dt1520.Authenticator.AspNetCore.Dt1520AuthenticatorCallbackValidator>.Instance),
            backendOptions,
            new InMemoryProtectedOperationStore(),
            TimeProvider.System);
    }

    private static HttpRequest CreateSignedCallbackRequest(string sessionId, string status)
    {
        var body = $$"""
            {
              "eventId": "8c2c3d76-31be-43f9-a751-67421528985d",
              "eventType": "challenge.{{status}}",
              "occurredAt": "2026-04-27T13:00:00Z",
              "challenge": {
                "id": "{{ChallengeId}}",
                "status": "{{status}}",
                "expiresAt": "2026-04-27T13:05:00Z",
                "correlationId": "{{sessionId}}"
              }
            }
            """;

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentLength = context.Request.Body.Length;
        context.Request.ContentType = "application/json";
        context.Request.Headers["X-OTPAuth-Signature"] = CreateSignature(body);
        return context.Request;
    }

    private static string CreateSignature(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("callback-secret"));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return $"sha256={Convert.ToHexString(signature).ToLowerInvariant()}";
    }

    private sealed class FakeAuthenticatorGateway : IReferenceAuthenticatorGateway
    {
        public ProtectedOperationRecord? LastCreateOperation { get; private set; }

        public Guid? PrimaryChallengeId { get; private set; }

        public Guid? TotpFallbackChallengeId { get; private set; }

        public IReadOnlyCollection<ChallengeFactorType>? PrimaryPreferredFactors { get; private set; }

        public IReadOnlyCollection<ChallengeFactorType>? TotpFallbackPreferredFactors { get; private set; }

        public Guid? LastVerifyChallengeId { get; private set; }

        public string? LastVerifyCode { get; private set; }

        public Task<Dt1520AuthenticatorResult<ChallengeResponse>> CreateChallengeAsync(
            ProtectedOperationRecord operation,
            CancellationToken cancellationToken)
        {
            LastCreateOperation = operation;
            PrimaryChallengeId = ChallengeId;
            PrimaryPreferredFactors = [ChallengeFactorType.Push, ChallengeFactorType.Totp];
            return Task.FromResult(Dt1520AuthenticatorResult<ChallengeResponse>.Success(CreateChallenge(
                ChallengeId,
                ChallengeFactorType.Push,
                "pending")));
        }

        public Task<Dt1520AuthenticatorResult<ChallengeResponse>> CreateTotpFallbackChallengeAsync(
            ProtectedOperationRecord operation,
            CancellationToken cancellationToken)
        {
            LastCreateOperation = operation;
            TotpFallbackChallengeId = TotpChallengeId;
            TotpFallbackPreferredFactors = [ChallengeFactorType.Totp];
            return Task.FromResult(Dt1520AuthenticatorResult<ChallengeResponse>.Success(CreateChallenge(
                TotpChallengeId,
                ChallengeFactorType.Totp,
                "pending")));
        }

        public Task<Dt1520AuthenticatorResult<ChallengeResponse>> VerifyTotpAsync(
            Guid challengeId,
            string code,
            CancellationToken cancellationToken)
        {
            LastVerifyChallengeId = challengeId;
            LastVerifyCode = code;
            return Task.FromResult(Dt1520AuthenticatorResult<ChallengeResponse>.Success(CreateChallenge(
                challengeId,
                ChallengeFactorType.Totp,
                "approved")));
        }

        private static ChallengeResponse CreateChallenge(
            Guid challengeId,
            ChallengeFactorType factorType,
            string status)
        {
            return new ChallengeResponse
            {
                Id = challengeId,
                TenantId = TenantId,
                ApplicationClientId = ApplicationClientId,
                FactorType = factorType,
                Status = Enum.Parse<ChallengeStatus>(status, ignoreCase: true),
                ExpiresAt = DateTimeOffset.Parse("2026-04-27T13:05:00Z"),
            };
        }
    }
}
