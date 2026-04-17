using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class RevokeTotpEnrollmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_RevokesActiveEnrollment()
    {
        var enrollment = CreateEnrollment();
        var store = new InMemoryProvisioningStore(enrollment);
        var auditWriter = new InMemoryAuditWriter();
        var handler = new RevokeTotpEnrollmentHandler(store, auditWriter);

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Enrollment);
        Assert.Equal(TotpEnrollmentStatus.Revoked, result.Enrollment!.Status);
        Assert.True(store.RevokeCalled);
        Assert.Single(auditWriter.RevokedEnrollmentIds);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenEnrollmentAlreadyRevoked()
    {
        var enrollment = CreateEnrollment() with
        {
            IsActive = false,
        };
        var handler = new RevokeTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RevokeTotpEnrollmentErrorCode.Conflict, result.ErrorCode);
        Assert.Equal($"Enrollment '{enrollment.EnrollmentId}' is already revoked.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var enrollment = CreateEnrollment();
        var handler = new RevokeTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            CreateClientContext(enrollment, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RevokeTotpEnrollmentErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'enrollments:write' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenEnrollmentBelongsToDifferentApplication()
    {
        var enrollment = CreateEnrollment();
        var handler = new RevokeTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());
        var clientContext = CreateClientContext(enrollment) with
        {
            ApplicationClientId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(
            enrollment.EnrollmentId,
            clientContext,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RevokeTotpEnrollmentErrorCode.NotFound, result.ErrorCode);
        Assert.Equal($"Enrollment '{enrollment.EnrollmentId}' was not found.", result.ErrorMessage);
    }

    private static TotpEnrollmentProvisioningRecord CreateEnrollment()
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

        public bool RevokeCalled { get; private set; }

        public Task<bool> ConfirmAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ConfirmReplacementAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByIdAsync(
            Guid enrollmentId,
            Guid tenantId,
            Guid applicationClientId,
            CancellationToken cancellationToken)
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

        public Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(
            Guid tenantId,
            Guid applicationClientId,
            string externalUserId,
            CancellationToken cancellationToken)
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

        public Task<bool> RevokeAsync(Guid enrollmentId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
        {
            RevokeCalled = true;
            return Task.FromResult(true);
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(
            TotpEnrollmentProvisioningDraft draft,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(TotpEnrollmentReplacementDraft draft, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryAuditWriter : ITotpEnrollmentAuditWriter
    {
        public List<Guid> RevokedEnrollmentIds { get; } = [];

        public Task WriteConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteRevokedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            RevokedEnrollmentIds.Add(enrollment.EnrollmentId);
            return Task.CompletedTask;
        }

        public Task WriteReplacementStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
