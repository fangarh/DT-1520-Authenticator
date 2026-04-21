using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class ReplaceTotpEnrollmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_StartsReplacementWithoutRevokingActiveEnrollment()
    {
        var enrollment = CreateConfirmedEnrollment();
        var store = new InMemoryProvisioningStore(enrollment);
        var auditWriter = new InMemoryAuditWriter();
        var handler = new ReplaceTotpEnrollmentHandler(store, auditWriter);

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Enrollment);
        Assert.Equal(TotpEnrollmentStatus.Confirmed, result.Enrollment!.Status);
        Assert.True(result.Enrollment.HasPendingReplacement);
        Assert.NotNull(result.Enrollment.SecretUri);
        Assert.True(store.ReplacementStarted);
        Assert.Single(auditWriter.StartedReplacementEnrollmentIds);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenEnrollmentIsPending()
    {
        var enrollment = CreateConfirmedEnrollment() with
        {
            ConfirmedUtc = null,
        };
        var handler = new ReplaceTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReplaceTotpEnrollmentErrorCode.Conflict, result.ErrorCode);
        Assert.Equal($"Enrollment '{enrollment.EnrollmentId}' is not confirmed and cannot be replaced.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenEnrollmentIsRevoked()
    {
        var enrollment = CreateConfirmedEnrollment() with
        {
            IsActive = false,
        };
        var handler = new ReplaceTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReplaceTotpEnrollmentErrorCode.Conflict, result.ErrorCode);
        Assert.Equal($"Enrollment '{enrollment.EnrollmentId}' is revoked and cannot be replaced.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var enrollment = CreateConfirmedEnrollment();
        var handler = new ReplaceTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReplaceTotpEnrollmentErrorCode.AccessDenied, result.ErrorCode);
    }

    private static TotpEnrollmentProvisioningRecord CreateConfirmedEnrollment()
    {
        return new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-123",
            Label = "ivan.petrov",
            Secret = [1, 2, 3],
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
            IsActive = true,
            ConfirmedUtc = DateTimeOffset.UtcNow,
            RevokedUtc = null,
            FailedConfirmationAttempts = 0,
            PendingReplacement = null,
        };
    }

    private static IntegrationClientContext CreateClientContext(
        TotpEnrollmentProvisioningRecord enrollment,
        IReadOnlyCollection<string>? scopes = null)
    {
        return new IntegrationClientContext
        {
            ClientId = "otpauth-crm",
            TenantId = enrollment.TenantId,
            ApplicationClientId = enrollment.ApplicationClientId,
            Scopes = scopes ?? [IntegrationClientScopes.EnrollmentsWrite],
        };
    }

    private sealed class InMemoryProvisioningStore : ITotpEnrollmentProvisioningStore
    {
        private readonly TotpEnrollmentProvisioningRecord _enrollment;

        public InMemoryProvisioningStore(TotpEnrollmentProvisioningRecord enrollment)
        {
            _enrollment = enrollment;
        }

        public bool ReplacementStarted { get; private set; }

        public Task<bool> ConfirmAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ConfirmReplacementAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByIdAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, CancellationToken cancellationToken)
        {
            if (_enrollment.EnrollmentId != enrollmentId ||
                _enrollment.TenantId != tenantId ||
                _enrollment.ApplicationClientId != applicationClientId)
            {
                return Task.FromResult<TotpEnrollmentProvisioningRecord?>(null);
            }

            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(_enrollment);
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByIdForAdminAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            if (_enrollment.EnrollmentId != enrollmentId)
            {
                return Task.FromResult<TotpEnrollmentProvisioningRecord?>(null);
            }

            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(_enrollment);
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetCurrentByExternalUserIdAsync(
            Guid tenantId,
            string externalUserId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task IncrementFailedConfirmationAttemptsAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task IncrementFailedReplacementConfirmationAttemptsAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RevokeAsync(Guid enrollmentId, DateTimeOffset revokedAt, FactorRevocationSideEffects? sideEffects, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(TotpEnrollmentProvisioningDraft draft, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(TotpEnrollmentReplacementDraft draft, CancellationToken cancellationToken)
        {
            ReplacementStarted = true;
            return Task.FromResult(_enrollment with
            {
                PendingReplacement = new TotpPendingReplacementRecord
                {
                    Secret = draft.Secret,
                    Digits = draft.Digits,
                    PeriodSeconds = draft.PeriodSeconds,
                    Algorithm = draft.Algorithm,
                    StartedUtc = DateTimeOffset.UtcNow,
                    FailedConfirmationAttempts = 0,
                },
            });
        }
    }

    private sealed class InMemoryAuditWriter : ITotpEnrollmentAuditWriter
    {
        public List<Guid> StartedReplacementEnrollmentIds { get; } = [];

        public Task WriteConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            StartedReplacementEnrollmentIds.Add(enrollment.EnrollmentId);
            return Task.CompletedTask;
        }

        public Task WriteRevokedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
