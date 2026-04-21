using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class ConfirmTotpEnrollmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_ConfirmsPendingEnrollment_WhenCodeIsValid()
    {
        var enrollment = CreatePendingEnrollment();
        var store = new InMemoryProvisioningStore(enrollment);
        var auditWriter = new InMemoryAuditWriter();
        var handler = new ConfirmTotpEnrollmentHandler(store, auditWriter);
        var code = TotpCodeCalculator.GenerateCode(
            enrollment.Secret,
            enrollment.Digits,
            enrollment.Algorithm,
            TotpCodeCalculator.GetTimeStep(DateTimeOffset.UtcNow, enrollment.PeriodSeconds));

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = code,
            },
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Enrollment);
        Assert.Equal(TotpEnrollmentStatus.Confirmed, result.Enrollment!.Status);
        Assert.False(result.Enrollment.HasPendingReplacement);
        Assert.True(store.ConfirmCalled);
        Assert.Single(auditWriter.ConfirmedEnrollmentIds);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenCodeIsInvalid()
    {
        var enrollment = CreatePendingEnrollment();
        var store = new InMemoryProvisioningStore(enrollment);
        var auditWriter = new InMemoryAuditWriter();
        var handler = new ConfirmTotpEnrollmentHandler(store, auditWriter);

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = "000000",
            },
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfirmTotpEnrollmentErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("Invalid one-time password.", result.ErrorMessage);
        Assert.Equal(1, store.FailedAttemptIncrements);
        Assert.Single(auditWriter.FailedEnrollmentIds);
    }

    [Fact]
    public async Task HandleAsync_ConfirmsPendingReplacement_WhenReplacementCodeIsValid()
    {
        var replacementSecret = new byte[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
        var enrollment = CreatePendingEnrollment() with
        {
            ConfirmedUtc = DateTimeOffset.UtcNow,
            PendingReplacement = new TotpPendingReplacementRecord
            {
                Secret = replacementSecret,
                Digits = 6,
                PeriodSeconds = 30,
                Algorithm = "SHA1",
                StartedUtc = DateTimeOffset.UtcNow,
                FailedConfirmationAttempts = 0,
            },
        };
        var store = new InMemoryProvisioningStore(enrollment);
        var auditWriter = new InMemoryAuditWriter();
        var handler = new ConfirmTotpEnrollmentHandler(store, auditWriter);
        var code = TotpCodeCalculator.GenerateCode(
            replacementSecret,
            6,
            "SHA1",
            TotpCodeCalculator.GetTimeStep(DateTimeOffset.UtcNow, 30));

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = code,
            },
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Enrollment);
        Assert.False(result.Enrollment!.HasPendingReplacement);
        Assert.True(store.ConfirmReplacementCalled);
        Assert.Single(auditWriter.ConfirmedReplacementEnrollmentIds);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenReplacementCodeIsInvalid()
    {
        var enrollment = CreatePendingEnrollment() with
        {
            ConfirmedUtc = DateTimeOffset.UtcNow,
            PendingReplacement = new TotpPendingReplacementRecord
            {
                Secret = new byte[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 },
                Digits = 6,
                PeriodSeconds = 30,
                Algorithm = "SHA1",
                StartedUtc = DateTimeOffset.UtcNow,
                FailedConfirmationAttempts = 0,
            },
        };
        var store = new InMemoryProvisioningStore(enrollment);
        var auditWriter = new InMemoryAuditWriter();
        var handler = new ConfirmTotpEnrollmentHandler(store, auditWriter);

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = "000000",
            },
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfirmTotpEnrollmentErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(1, store.FailedReplacementAttemptIncrements);
        Assert.Single(auditWriter.FailedReplacementEnrollmentIds);
    }

    [Fact]
    public async Task HandleAsync_LocksEnrollment_WhenAttemptLimitIsReached()
    {
        var enrollment = CreatePendingEnrollment() with
        {
            FailedConfirmationAttempts = 4,
        };
        var store = new InMemoryProvisioningStore(enrollment);
        var handler = new ConfirmTotpEnrollmentHandler(store, new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = "000000",
            },
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfirmTotpEnrollmentErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Too many invalid confirmation attempts. Restart enrollment.", result.ErrorMessage);
        Assert.Equal(1, store.FailedAttemptIncrements);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenEnrollmentIsAlreadyConfirmed()
    {
        var enrollment = CreatePendingEnrollment() with
        {
            ConfirmedUtc = DateTimeOffset.UtcNow,
        };
        var handler = new ConfirmTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = "123456",
            },
            CreateClientContext(enrollment),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfirmTotpEnrollmentErrorCode.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var enrollment = CreatePendingEnrollment();
        var handler = new ConfirmTotpEnrollmentHandler(
            new InMemoryProvisioningStore(enrollment),
            new InMemoryAuditWriter());

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = "123456",
            },
            CreateClientContext(enrollment, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfirmTotpEnrollmentErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'enrollments:write' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenEnrollmentBelongsToDifferentTenant()
    {
        var enrollment = CreatePendingEnrollment();
        var store = new InMemoryProvisioningStore(enrollment);
        var handler = new ConfirmTotpEnrollmentHandler(store, new InMemoryAuditWriter());
        var clientContext = CreateClientContext(enrollment) with
        {
            TenantId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(
            new ConfirmTotpEnrollmentRequest
            {
                EnrollmentId = enrollment.EnrollmentId,
                Code = "123456",
            },
            clientContext,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfirmTotpEnrollmentErrorCode.NotFound, result.ErrorCode);
    }

    private static TotpEnrollmentProvisioningRecord CreatePendingEnrollment()
    {
        return new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-123",
            Label = "ivan.petrov",
            Secret = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
            IsActive = true,
            ConfirmedUtc = null,
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

        public bool ConfirmCalled { get; private set; }

        public bool ConfirmReplacementCalled { get; private set; }

        public int FailedAttemptIncrements { get; private set; }

        public int FailedReplacementAttemptIncrements { get; private set; }

        public Task<bool> ConfirmAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            ConfirmCalled = true;
            return Task.FromResult(true);
        }

        public Task<bool> ConfirmReplacementAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            ConfirmReplacementCalled = true;
            return Task.FromResult(true);
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
            FailedAttemptIncrements++;
            return Task.CompletedTask;
        }

        public Task IncrementFailedReplacementConfirmationAttemptsAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            FailedReplacementAttemptIncrements++;
            return Task.CompletedTask;
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
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryAuditWriter : ITotpEnrollmentAuditWriter
    {
        public List<Guid> ConfirmedEnrollmentIds { get; } = [];

        public List<Guid> FailedEnrollmentIds { get; } = [];

        public List<Guid> ConfirmedReplacementEnrollmentIds { get; } = [];

        public List<Guid> FailedReplacementEnrollmentIds { get; } = [];

        public Task WriteConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            FailedEnrollmentIds.Add(enrollmentId);
            return Task.CompletedTask;
        }

        public Task WriteConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            ConfirmedEnrollmentIds.Add(enrollment.EnrollmentId);
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            ConfirmedReplacementEnrollmentIds.Add(enrollment.EnrollmentId);
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            FailedReplacementEnrollmentIds.Add(enrollmentId);
            return Task.CompletedTask;
        }

        public Task WriteReplacementStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
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
