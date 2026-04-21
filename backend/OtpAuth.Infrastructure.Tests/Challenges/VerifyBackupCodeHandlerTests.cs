using OtpAuth.Application.Challenges;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class VerifyBackupCodeHandlerTests
{
    [Fact]
    public async Task HandleAsync_ApprovesPendingBackupCodeChallenge_WhenCodeIsValid()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyBackupCodeHandler(
            repository,
            recorder,
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD-1234"),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Approved, result.Challenge!.Status);
        Assert.NotNull(result.Challenge.ApprovedUtc);
        Assert.Equal(ChallengeAttemptResults.Approved, Assert.Single(recorder.Attempts).Result);
        var callbackDelivery = Assert.Single(repository.GetCallbackDeliveries());
        Assert.Equal(ChallengeCallbackEventType.Approved, callbackDelivery.EventType);
    }

    [Fact]
    public async Task HandleAsync_FailsChallenge_WhenCodeIsInvalid()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyBackupCodeHandler(
            repository,
            recorder,
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.InvalidCode()));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.InvalidCode, result.ErrorCode);
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
        var verifier = new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid()));
        var handler = new VerifyBackupCodeHandler(
            repository,
            recorder,
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Denied(600)),
            verifier);

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.RateLimited, result.ErrorCode);
        Assert.Equal(600, result.RetryAfterSeconds);
        Assert.Null(verifier.LastRequest);
        Assert.Equal(ChallengeAttemptResults.RateLimited, Assert.Single(recorder.Attempts).Result);
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
        var handler = new VerifyBackupCodeHandler(
            repository,
            recorder,
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.ChallengeExpired, result.ErrorCode);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Expired, result.Challenge!.Status);
        Assert.Equal(ChallengeAttemptResults.Expired, Assert.Single(recorder.Attempts).Result);
        var callbackDelivery = Assert.Single(repository.GetCallbackDeliveries());
        Assert.Equal(ChallengeCallbackEventType.Expired, callbackDelivery.EventType);
    }

    [Fact]
    public async Task HandleAsync_RejectsNonBackupCodeChallenge()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            FactorType = FactorType.Totp,
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var recorder = new InMemoryChallengeAttemptRecorder();
        var handler = new VerifyBackupCodeHandler(
            repository,
            recorder,
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.InvalidState, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' does not support backup code verification.", result.ErrorMessage);
        Assert.Equal(ChallengeAttemptResults.UnsupportedFactor, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_RejectsInvalidCodeFormat()
    {
        var handler = new VerifyBackupCodeHandler(
            new InMemoryChallengeRepository(),
            new InMemoryChallengeAttemptRecorder(),
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            CreateRequest(Guid.NewGuid(), "12 34"),
            CreateClientContext(CreateChallenge()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("Backup code must be 8-32 alphanumeric characters.", result.ErrorMessage);
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
        var handler = new VerifyBackupCodeHandler(
            repository,
            recorder,
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            CreateClientContext(challenge),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.InvalidState, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' is not pending.", result.ErrorMessage);
        Assert.Equal(ChallengeAttemptResults.InvalidState, Assert.Single(recorder.Attempts).Result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var challenge = CreateChallenge();
        var handler = new VerifyBackupCodeHandler(
            new InMemoryChallengeRepository(),
            new InMemoryChallengeAttemptRecorder(),
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            CreateClientContext(challenge, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'challenges:write' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenChallengeBelongsToDifferentTenant()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var handler = new VerifyBackupCodeHandler(
            repository,
            new InMemoryChallengeAttemptRecorder(),
            new StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision.Allowed()),
            new StubBackupCodeVerifier(BackupCodeVerificationResult.Valid(Guid.NewGuid())));
        var clientContext = CreateClientContext(challenge) with
        {
            TenantId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(
            CreateRequest(challenge.Id, "ABCD1234"),
            clientContext,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyBackupCodeErrorCode.NotFound, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' was not found.", result.ErrorMessage);
    }

    private static VerifyBackupCodeRequest CreateRequest(Guid challengeId, string code)
    {
        return new VerifyBackupCodeRequest
        {
            ChallengeId = challengeId,
            Code = code,
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
            OperationType = OperationType.BackupCodeRecovery,
            OperationDisplayName = "Recover ERP access",
            FactorType = FactorType.BackupCode,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "auth-req-2026-04-17-001",
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

    private sealed class StubBackupCodeVerificationRateLimiter : IBackupCodeVerificationRateLimiter
    {
        private readonly BackupCodeVerificationRateLimitDecision _decision;

        public StubBackupCodeVerificationRateLimiter(BackupCodeVerificationRateLimitDecision decision)
        {
            _decision = decision;
        }

        public Task<BackupCodeVerificationRateLimitDecision> EvaluateAsync(
            Challenge challenge,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_decision);
        }
    }

    private sealed class StubBackupCodeVerifier : IBackupCodeVerifier
    {
        private readonly BackupCodeVerificationResult _result;

        public (Guid ChallengeId, string Code)? LastRequest { get; private set; }

        public StubBackupCodeVerifier(BackupCodeVerificationResult result)
        {
            _result = result;
        }

        public Task<BackupCodeVerificationResult> VerifyAsync(
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
