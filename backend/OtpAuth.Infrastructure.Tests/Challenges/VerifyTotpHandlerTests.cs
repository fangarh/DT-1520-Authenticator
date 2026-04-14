using OtpAuth.Application.Challenges;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class VerifyTotpHandlerTests
{
    [Fact]
    public async Task HandleAsync_ApprovesPendingTotpChallenge_WhenCodeIsValid()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var rateLimiter = new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed());
        var verifier = new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L));
        var handler = new VerifyTotpHandler(repository, recorder, rateLimiter, verifier);

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Approved, result.Challenge!.Status);
        Assert.Equal(ChallengeAttemptResults.Approved, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_FailsChallenge_WhenCodeIsInvalid()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyTotpHandler(
            repository,
            recorder,
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.InvalidCode()));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.InvalidCode, result.ErrorCode);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Failed, result.Challenge!.Status);
        Assert.Equal(ChallengeAttemptResults.InvalidCode, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsRateLimited_WhenWindowIsExceeded()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var verifier = new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L));
        var handler = new VerifyTotpHandler(
            repository,
            recorder,
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Denied(600)),
            verifier);

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.RateLimited, result.ErrorCode);
        Assert.Equal(600, result.RetryAfterSeconds);
        Assert.Null(verifier.LastRequest);
        Assert.Equal(ChallengeAttemptResults.RateLimited, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_FailsChallengeAndHidesReplay_WhenCodeWasAlreadyUsed()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyTotpHandler(
            repository,
            recorder,
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.ReplayDetected(Guid.NewGuid(), 987654L)));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.InvalidCode, result.ErrorCode);
        Assert.Equal("Invalid one-time password.", result.ErrorMessage);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Failed, result.Challenge!.Status);
        Assert.Equal(ChallengeAttemptResults.ReplayDetected, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_ExpiresChallenge_WhenLifetimeElapsed()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyTotpHandler(
            repository,
            recorder,
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.ChallengeExpired, result.ErrorCode);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Expired, result.Challenge!.Status);
        Assert.Equal(ChallengeAttemptResults.Expired, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_RejectsNonTotpChallenge()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            FactorType = FactorType.Push,
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyTotpHandler(
            repository,
            recorder,
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.InvalidState, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' does not support TOTP verification.", result.ErrorMessage);
        Assert.Equal(ChallengeAttemptResults.UnsupportedFactor, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_RejectsInvalidCodeFormat()
    {
        var handler = new VerifyTotpHandler(
            new InMemoryChallengeRepository(),
            new InMemoryChallengeAttemptRecorder(),
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));

        var result = await handler.HandleAsync(
            new VerifyTotpRequest
            {
                ChallengeId = Guid.NewGuid(),
                Code = "12ab",
            },
            CreateClientContext(CreateChallenge()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("Code must be a 6-digit numeric value.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_RejectsAlreadyCompletedChallenge()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            Status = ChallengeStatus.Approved,
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyTotpHandler(
            repository,
            recorder,
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.InvalidState, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' is not pending.", result.ErrorMessage);
        Assert.Equal(ChallengeAttemptResults.InvalidState, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var challenge = CreateChallenge();
        var handler = new VerifyTotpHandler(
            new InMemoryChallengeRepository(),
            new InMemoryChallengeAttemptRecorder(),
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            CreateClientContext(challenge, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'challenges:write' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenChallengeBelongsToDifferentTenant()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var handler = new VerifyTotpHandler(
            repository,
            new InMemoryChallengeAttemptRecorder(),
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));
        var clientContext = CreateClientContext(challenge) with
        {
            TenantId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id),
            clientContext,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyTotpErrorCode.NotFound, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' was not found.", result.ErrorMessage);
    }

    private static VerifyTotpRequest CreateRequest(Guid challengeId)
    {
        return new VerifyTotpRequest
        {
            ChallengeId = challengeId,
            Code = "123456",
        };
    }

    private static Challenge CreateChallenge()
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-789",
            Username = "maria.smirnova",
            OperationType = OperationType.Login,
            OperationDisplayName = "Sign in to ERP",
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "auth-req-2026-04-14-003",
            CallbackUrl = new Uri("https://erp.example.com/webhooks/challenges"),
        };
    }

    private static IntegrationClientContext CreateClientContext(
        Challenge challenge,
        IReadOnlyCollection<string>? scopes = null)
    {
        return new IntegrationClientContext
        {
            ClientId = "otpauth-crm",
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            Scopes = scopes ?? [IntegrationClientScopes.ChallengesWrite, IntegrationClientScopes.ChallengesRead],
        };
    }

    private sealed class InMemoryChallengeAttemptRecorder : IChallengeAttemptRecorder
    {
        public List<ChallengeAttemptRecord> Attempts { get; } = [];

        public Task RecordAsync(ChallengeAttemptRecord attempt, CancellationToken cancellationToken)
        {
            Attempts.Add(attempt);
            return Task.CompletedTask;
        }
    }

    private sealed class StubTotpVerificationRateLimiter : ITotpVerificationRateLimiter
    {
        private readonly TotpVerificationRateLimitDecision _decision;

        public StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision decision)
        {
            _decision = decision;
        }

        public Task<TotpVerificationRateLimitDecision> EvaluateAsync(
            Challenge challenge,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_decision);
        }
    }

    private sealed class StubTotpVerifier : ITotpVerifier
    {
        private readonly TotpVerificationResult _result;

        public (Guid ChallengeId, string Code)? LastRequest { get; private set; }

        public StubTotpVerifier(TotpVerificationResult result)
        {
            _result = result;
        }

        public Task<TotpVerificationResult> VerifyAsync(
            Challenge challenge,
            string code,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            LastRequest = (challenge.Id, code);
            return Task.FromResult(_result);
        }
    }
}
