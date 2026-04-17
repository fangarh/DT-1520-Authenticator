using OtpAuth.Application.Factors;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class PostgresBackupCodeVerifierTests
{
    [Fact]
    public async Task VerifyAsync_ReturnsValid_AndMarksBackupCodeUsed_WhenCodeMatches()
    {
        var hasher = new Pbkdf2BackupCodeHasher();
        var store = new FakeBackupCodeStore
        {
            ActiveCodes =
            [
                new BackupCodeCredential
                {
                    BackupCodeId = Guid.NewGuid(),
                    CodeHash = hasher.Hash("ABCD1234"),
                },
            ],
        };
        var verifier = new PostgresBackupCodeVerifier(store, hasher);

        var result = await verifier.VerifyAsync(CreateChallenge(), "ABCD-1234", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(BackupCodeVerificationStatus.Valid, result.Status);
        Assert.Equal(store.ActiveCodes[0].BackupCodeId, store.LastMarkedBackupCodeId);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalidCode_WhenNoActiveCodesExist()
    {
        var verifier = new PostgresBackupCodeVerifier(new FakeBackupCodeStore(), new Pbkdf2BackupCodeHasher());

        var result = await verifier.VerifyAsync(CreateChallenge(), "ABCD1234", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(BackupCodeVerificationStatus.InvalidCode, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalidCode_WhenCodeDoesNotMatch()
    {
        var hasher = new Pbkdf2BackupCodeHasher();
        var store = new FakeBackupCodeStore
        {
            ActiveCodes =
            [
                new BackupCodeCredential
                {
                    BackupCodeId = Guid.NewGuid(),
                    CodeHash = hasher.Hash("ABCD1234"),
                },
            ],
        };
        var verifier = new PostgresBackupCodeVerifier(store, hasher);

        var result = await verifier.VerifyAsync(CreateChallenge(), "ZXCV5678", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(BackupCodeVerificationStatus.InvalidCode, result.Status);
        Assert.Null(store.LastMarkedBackupCodeId);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalidCode_WhenMatchedCodeWasConsumedConcurrently()
    {
        var hasher = new Pbkdf2BackupCodeHasher();
        var store = new FakeBackupCodeStore
        {
            TryMarkUsedResult = false,
            ActiveCodes =
            [
                new BackupCodeCredential
                {
                    BackupCodeId = Guid.NewGuid(),
                    CodeHash = hasher.Hash("ABCD1234"),
                },
            ],
        };
        var verifier = new PostgresBackupCodeVerifier(store, hasher);

        var result = await verifier.VerifyAsync(CreateChallenge(), "ABCD1234", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(BackupCodeVerificationStatus.InvalidCode, result.Status);
        Assert.Equal(store.ActiveCodes[0].BackupCodeId, store.LastMarkedBackupCodeId);
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
            OperationType = OperationType.BackupCodeRecovery,
            OperationDisplayName = "Recover account",
            FactorType = FactorType.BackupCode,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "corr-e2e",
            CallbackUrl = null,
        };
    }

    private sealed class FakeBackupCodeStore : IBackupCodeStore
    {
        public List<BackupCodeCredential> ActiveCodes { get; init; } = [];

        public Guid? LastMarkedBackupCodeId { get; private set; }

        public bool TryMarkUsedResult { get; init; } = true;

        public Task<IReadOnlyCollection<BackupCodeCredential>> ListActiveAsync(
            Guid tenantId,
            Guid applicationClientId,
            string externalUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<BackupCodeCredential>>(ActiveCodes);
        }

        public Task<bool> TryMarkUsedAsync(
            Guid backupCodeId,
            DateTimeOffset usedAt,
            CancellationToken cancellationToken)
        {
            LastMarkedBackupCodeId = backupCodeId;
            return Task.FromResult(TryMarkUsedResult);
        }
    }
}
