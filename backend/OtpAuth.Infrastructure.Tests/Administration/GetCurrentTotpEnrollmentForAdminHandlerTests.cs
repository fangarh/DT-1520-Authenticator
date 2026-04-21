using OtpAuth.Application.Administration;
using OtpAuth.Application.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class GetCurrentTotpEnrollmentForAdminHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsEnrollmentView_WhenAdminHasPermission()
    {
        var expectedEnrollment = new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = Guid.NewGuid(),
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
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
        var handler = new GetCurrentTotpEnrollmentForAdminHandler(
            new StubProvisioningStore(expectedEnrollment));

        var result = await handler.HandleAsync(
            expectedEnrollment.TenantId,
            "user-123",
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.EnrollmentsRead],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Enrollment);
        Assert.Equal(expectedEnrollment.EnrollmentId, result.Enrollment!.EnrollmentId);
        Assert.Equal(expectedEnrollment.ApplicationClientId, result.Enrollment.ApplicationClientId);
        Assert.Equal(expectedEnrollment.ConfirmedUtc, result.Enrollment.ConfirmedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = new GetCurrentTotpEnrollmentForAdminHandler(
            new StubProvisioningStore(null));

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            "user-123",
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetCurrentTotpEnrollmentForAdminErrorCode.AccessDenied, result.ErrorCode);
    }

    private sealed class StubProvisioningStore : ITotpEnrollmentProvisioningStore
    {
        private readonly TotpEnrollmentProvisioningRecord? _enrollment;

        public StubProvisioningStore(TotpEnrollmentProvisioningRecord? enrollment)
        {
            _enrollment = enrollment;
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByIdAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, CancellationToken cancellationToken)
        {
            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(null);
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByIdForAdminAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(null);
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(null);
        }

        public Task<TotpEnrollmentProvisioningRecord?> GetCurrentByExternalUserIdAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_enrollment);
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(TotpEnrollmentProvisioningDraft draft, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(TotpEnrollmentReplacementDraft draft, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ConfirmAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ConfirmReplacementAsync(Guid enrollmentId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RevokeAsync(Guid enrollmentId, DateTimeOffset revokedAt, FactorRevocationSideEffects? sideEffects, CancellationToken cancellationToken)
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
    }
}
