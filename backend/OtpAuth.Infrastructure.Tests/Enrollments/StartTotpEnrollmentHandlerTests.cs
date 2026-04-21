using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Policy;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class StartTotpEnrollmentHandlerTests
{
    private static readonly Guid ApplicationClientId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_CreatesPendingEnrollment_WhenRequestIsValid()
    {
        var store = new InMemoryProvisioningStore();
        var auditWriter = new InMemoryAuditWriter();
        var handler = new StartTotpEnrollmentHandler(store, new DefaultPolicyEvaluator(), auditWriter);
        var request = CreateRequest();

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Enrollment);
        Assert.Equal(TotpEnrollmentStatus.Pending, result.Enrollment!.Status);
        Assert.False(result.Enrollment.HasPendingReplacement);
        Assert.NotNull(result.Enrollment.SecretUri);
        Assert.Contains("otpauth://totp/", result.Enrollment.SecretUri!, StringComparison.Ordinal);
        Assert.Contains("issuer=OTPAuth", result.Enrollment.SecretUri!, StringComparison.Ordinal);
        Assert.Contains("digits=6", result.Enrollment.SecretUri!, StringComparison.Ordinal);
        Assert.Contains("period=30", result.Enrollment.SecretUri!, StringComparison.Ordinal);
        Assert.Contains("algorithm=SHA1", result.Enrollment.SecretUri!, StringComparison.Ordinal);
        Assert.Single(auditWriter.StartedEnrollmentIds);
        Assert.Single(store.UpsertedEnrollments);
    }

    [Fact]
    public async Task HandleAsync_DeniesRequest_WhenScopeIsMissing()
    {
        var handler = new StartTotpEnrollmentHandler(
            new InMemoryProvisioningStore(),
            new DefaultPolicyEvaluator(),
            new InMemoryAuditWriter());
        var request = CreateRequest();

        var result = await handler.HandleAsync(
            request,
            CreateClientContext(request, Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StartTotpEnrollmentErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'enrollments:write' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_DeniesRequest_WhenTenantDoesNotMatchAuthenticatedClient()
    {
        var handler = new StartTotpEnrollmentHandler(
            new InMemoryProvisioningStore(),
            new DefaultPolicyEvaluator(),
            new InMemoryAuditWriter());
        var request = CreateRequest();
        var clientContext = CreateClientContext(request) with
        {
            TenantId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(request, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StartTotpEnrollmentErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Request tenant is outside the authenticated client scope.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenActiveEnrollmentAlreadyExists()
    {
        var request = CreateRequest();
        var store = new InMemoryProvisioningStore
        {
            EnrollmentByExternalUserId = new TotpEnrollmentProvisioningRecord
            {
                EnrollmentId = Guid.NewGuid(),
                TenantId = request.TenantId,
                ApplicationClientId = ApplicationClientId,
                ExternalUserId = request.ExternalUserId,
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
            },
        };
        var handler = new StartTotpEnrollmentHandler(store, new DefaultPolicyEvaluator(), new InMemoryAuditWriter());

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(StartTotpEnrollmentErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("An active TOTP enrollment already exists for the subject.", result.ErrorMessage);
        Assert.Empty(store.UpsertedEnrollments);
    }

    [Fact]
    public async Task HandleAsync_ResetsPendingEnrollment_WhenPendingEnrollmentAlreadyExists()
    {
        var request = CreateRequest();
        var existingEnrollmentId = Guid.NewGuid();
        var store = new InMemoryProvisioningStore
        {
            EnrollmentByExternalUserId = new TotpEnrollmentProvisioningRecord
            {
                EnrollmentId = existingEnrollmentId,
                TenantId = request.TenantId,
                ApplicationClientId = ApplicationClientId,
                ExternalUserId = request.ExternalUserId,
                Label = "old-label",
                Secret = [1, 2, 3],
                Digits = 6,
                PeriodSeconds = 30,
                Algorithm = "SHA1",
                IsActive = true,
                ConfirmedUtc = null,
                RevokedUtc = null,
                FailedConfirmationAttempts = 4,
                PendingReplacement = null,
            },
        };
        var handler = new StartTotpEnrollmentHandler(store, new DefaultPolicyEvaluator(), new InMemoryAuditWriter());

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingEnrollmentId, result.Enrollment!.EnrollmentId);
        Assert.Single(store.UpsertedEnrollments);
        Assert.Equal("ivan.petrov", store.UpsertedEnrollments.Single().Label);
    }

    private static StartTotpEnrollmentRequest CreateRequest()
    {
        return new StartTotpEnrollmentRequest
        {
            TenantId = Guid.NewGuid(),
            ExternalUserId = "user-123",
            Issuer = "OTPAuth",
            Label = "ivan.petrov",
        };
    }

    private static IntegrationClientContext CreateClientContext(
        StartTotpEnrollmentRequest request,
        IReadOnlyCollection<string>? scopes = null)
    {
        return new IntegrationClientContext
        {
            ClientId = "otpauth-crm",
            TenantId = request.TenantId,
            ApplicationClientId = ApplicationClientId,
            Scopes = scopes ?? [IntegrationClientScopes.EnrollmentsWrite],
        };
    }

    private sealed class InMemoryProvisioningStore : ITotpEnrollmentProvisioningStore
    {
        public TotpEnrollmentProvisioningRecord? EnrollmentByExternalUserId { get; set; }

        public List<TotpEnrollmentProvisioningDraft> UpsertedEnrollments { get; } = [];

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
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByIdForAdminAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(
            Guid tenantId,
            Guid applicationClientId,
            string externalUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(EnrollmentByExternalUserId);
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

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(
            TotpEnrollmentProvisioningDraft draft,
            CancellationToken cancellationToken)
        {
            UpsertedEnrollments.Add(draft);
            var existingId = EnrollmentByExternalUserId?.EnrollmentId ?? Guid.NewGuid();
            return Task.FromResult(new TotpEnrollmentProvisioningRecord
            {
                EnrollmentId = existingId,
                TenantId = draft.TenantId,
                ApplicationClientId = draft.ApplicationClientId,
                ExternalUserId = draft.ExternalUserId,
                Label = draft.Label,
                Secret = draft.Secret,
                Digits = draft.Digits,
                PeriodSeconds = draft.PeriodSeconds,
                Algorithm = draft.Algorithm,
                IsActive = true,
                ConfirmedUtc = null,
                RevokedUtc = null,
                FailedConfirmationAttempts = 0,
                PendingReplacement = null,
            });
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(TotpEnrollmentReplacementDraft draft, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryAuditWriter : ITotpEnrollmentAuditWriter
    {
        public List<Guid> StartedEnrollmentIds { get; } = [];

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
            StartedEnrollmentIds.Add(enrollment.EnrollmentId);
            return Task.CompletedTask;
        }
    }
}
