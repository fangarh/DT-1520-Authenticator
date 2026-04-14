using OtpAuth.Application.Factors;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class PostgresTotpVerifierTests
{
    [Fact]
    public async Task VerifyAsync_ReturnsValid_AndMarksEnrollmentUsed_WhenCodeMatches()
    {
        var enrollmentStore = new FakeTotpEnrollmentStore
        {
            Enrollment = CreateEnrollment(),
        };
        var replayProtector = new FakeTotpReplayProtector();
        var verifier = new PostgresTotpVerifier(enrollmentStore, replayProtector);
        var challenge = CreateChallenge();
        var code = PostgresTotpVerifier.GenerateCode(enrollmentStore.Enrollment!, DateTimeOffset.UtcNow);

        var result = await verifier.VerifyAsync(challenge, code, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(TotpVerificationStatus.Valid, result.Status);
        Assert.Equal(enrollmentStore.Enrollment!.EnrollmentId, enrollmentStore.LastMarkedEnrollmentId);
        Assert.NotNull(replayProtector.LastReservation);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalidCode_WhenEnrollmentIsMissing()
    {
        var verifier = new PostgresTotpVerifier(new FakeTotpEnrollmentStore(), new FakeTotpReplayProtector());

        var result = await verifier.VerifyAsync(CreateChallenge(), "123456", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(TotpVerificationStatus.InvalidCode, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalidCode_WhenCodeDoesNotMatch()
    {
        var enrollmentStore = new FakeTotpEnrollmentStore
        {
            Enrollment = CreateEnrollment(),
        };
        var replayProtector = new FakeTotpReplayProtector();
        var verifier = new PostgresTotpVerifier(enrollmentStore, replayProtector);

        var result = await verifier.VerifyAsync(CreateChallenge(), "000000", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(TotpVerificationStatus.InvalidCode, result.Status);
        Assert.Null(enrollmentStore.LastMarkedEnrollmentId);
        Assert.Null(replayProtector.LastReservation);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsReplayDetected_WhenMatchingCodeWasAlreadyReserved()
    {
        var enrollmentStore = new FakeTotpEnrollmentStore
        {
            Enrollment = CreateEnrollment(),
        };
        var replayProtector = new FakeTotpReplayProtector
        {
            TryReserveResult = false,
        };
        var verifier = new PostgresTotpVerifier(enrollmentStore, replayProtector);
        var timestamp = DateTimeOffset.UtcNow;
        var code = PostgresTotpVerifier.GenerateCode(enrollmentStore.Enrollment!, timestamp);

        var result = await verifier.VerifyAsync(CreateChallenge(), code, timestamp, CancellationToken.None);

        Assert.Equal(TotpVerificationStatus.ReplayDetected, result.Status);
        Assert.Null(enrollmentStore.LastMarkedEnrollmentId);
    }

    private static Challenge CreateChallenge()
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
            ExternalUserId = "user-e2e-001",
            Username = "e2e.user",
            OperationType = OperationType.Login,
            OperationDisplayName = "Sign in",
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "corr-e2e",
            CallbackUrl = null,
        };
    }

    private static TotpEnrollmentSecret CreateEnrollment()
    {
        return new TotpEnrollmentSecret
        {
            EnrollmentId = Guid.NewGuid(),
            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
            ExternalUserId = "user-e2e-001",
            Username = "e2e.user",
            Secret = "ABCDEFGHIJKLMNOPQRSTUVWX12345678"u8.ToArray(),
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
            KeyVersion = 1,
        };
    }

    private sealed class FakeTotpEnrollmentStore : ITotpEnrollmentStore
    {
        public TotpEnrollmentSecret? Enrollment { get; init; }

        public Guid? LastMarkedEnrollmentId { get; private set; }

        public Task<TotpEnrollmentSecret?> GetActiveAsync(
            Guid tenantId,
            Guid applicationClientId,
            string externalUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Enrollment);
        }

        public Task MarkUsedAsync(Guid enrollmentId, DateTimeOffset usedAt, CancellationToken cancellationToken)
        {
            LastMarkedEnrollmentId = enrollmentId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTotpReplayProtector : ITotpReplayProtector
    {
        public bool TryReserveResult { get; init; } = true;

        public (Guid EnrollmentId, long TimeStep, DateTimeOffset UsedAt, DateTimeOffset ExpiresAt)? LastReservation { get; private set; }

        public Task<bool> TryReserveAsync(
            Guid enrollmentId,
            long timeStep,
            DateTimeOffset usedAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            LastReservation = (enrollmentId, timeStep, usedAt, expiresAt);
            return Task.FromResult(TryReserveResult);
        }
    }
}
