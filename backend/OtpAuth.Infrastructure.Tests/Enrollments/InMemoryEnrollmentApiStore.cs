using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

internal static class EnrollmentApiTestContext
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

public sealed class InMemoryEnrollmentApiStore : ITotpEnrollmentProvisioningStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, TotpEnrollmentProvisioningRecord> _enrollments = [];
    private readonly Dictionary<Guid, WebhookSubscription> _subscriptions = [];
    private readonly Dictionary<Guid, WebhookEventDelivery> _webhookDeliveries = [];

    public TotpEnrollmentProvisioningRecord SeedPending(
        string externalUserId = "user-123",
        int failedConfirmationAttempts = 0,
        Guid? applicationClientId = null)
    {
        var enrollment = CreateEnrollment(
            externalUserId,
            isActive: true,
            confirmedUtc: null,
            failedConfirmationAttempts: failedConfirmationAttempts,
            pendingReplacement: null,
            applicationClientId: applicationClientId);

        Store(enrollment);
        return enrollment;
    }

    public TotpEnrollmentProvisioningRecord SeedConfirmed(
        string externalUserId = "user-123",
        TotpPendingReplacementRecord? pendingReplacement = null,
        Guid? applicationClientId = null)
    {
        var enrollment = CreateEnrollment(
            externalUserId,
            isActive: true,
            confirmedUtc: DateTimeOffset.UtcNow,
            failedConfirmationAttempts: 0,
            pendingReplacement: pendingReplacement,
            applicationClientId: applicationClientId);

        Store(enrollment);
        return enrollment;
    }

    public TotpEnrollmentProvisioningRecord SeedRevoked(string externalUserId = "user-123", Guid? applicationClientId = null)
    {
        var enrollment = CreateEnrollment(
            externalUserId,
            isActive: false,
            confirmedUtc: DateTimeOffset.UtcNow,
            failedConfirmationAttempts: 0,
            pendingReplacement: null,
            applicationClientId: applicationClientId);

        Store(enrollment);
        return enrollment;
    }

    public Task<TotpEnrollmentProvisioningRecord?> GetByIdAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (_enrollments.TryGetValue(enrollmentId, out var enrollment) &&
                enrollment.TenantId == tenantId &&
                enrollment.ApplicationClientId == applicationClientId)
            {
                return Task.FromResult<TotpEnrollmentProvisioningRecord?>(enrollment);
            }
        }

        return Task.FromResult<TotpEnrollmentProvisioningRecord?>(null);
    }

    public Task<TotpEnrollmentProvisioningRecord?> GetByIdForAdminAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _enrollments.TryGetValue(enrollmentId, out var enrollment);
            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(enrollment);
        }
    }

    public Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            var enrollment = _enrollments.Values.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.ApplicationClientId == applicationClientId &&
                string.Equals(item.ExternalUserId, externalUserId, StringComparison.Ordinal));

            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(enrollment);
        }
    }

    public Task<TotpEnrollmentProvisioningRecord?> GetCurrentByExternalUserIdAsync(
        Guid tenantId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            var enrollment = _enrollments.Values
                .Where(item =>
                    item.TenantId == tenantId &&
                    string.Equals(item.ExternalUserId, externalUserId, StringComparison.Ordinal))
                .OrderByDescending(static item => item.IsActive)
                .ThenByDescending(static item => item.ConfirmedUtc)
                .ThenByDescending(static item => item.RevokedUtc)
                .ThenByDescending(static item => item.EnrollmentId)
                .FirstOrDefault();

            return Task.FromResult<TotpEnrollmentProvisioningRecord?>(enrollment);
        }
    }

    public Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(
        TotpEnrollmentProvisioningDraft draft,
        CancellationToken cancellationToken)
    {
        var enrollment = CreateEnrollment(
            draft.ExternalUserId,
            isActive: true,
            confirmedUtc: null,
            failedConfirmationAttempts: 0,
            pendingReplacement: null) with
        {
            TenantId = draft.TenantId,
            ApplicationClientId = draft.ApplicationClientId,
            Label = draft.Label,
            Secret = draft.Secret,
            Digits = draft.Digits,
            PeriodSeconds = draft.PeriodSeconds,
            Algorithm = draft.Algorithm,
        };

        Store(enrollment);
        return Task.FromResult(enrollment);
    }

    public Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(
        TotpEnrollmentReplacementDraft draft,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            var enrollment = _enrollments[draft.EnrollmentId];
            var updated = enrollment with
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
            };

            _enrollments[draft.EnrollmentId] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<bool> ConfirmAsync(
        Guid enrollmentId,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!_enrollments.TryGetValue(enrollmentId, out var enrollment) ||
                !enrollment.IsActive ||
                enrollment.ConfirmedUtc.HasValue)
            {
                return Task.FromResult(false);
            }

            _enrollments[enrollmentId] = enrollment with
            {
                ConfirmedUtc = confirmedAt,
                RevokedUtc = null,
                FailedConfirmationAttempts = 0,
            };

            return Task.FromResult(true);
        }
    }

    public Task<bool> ConfirmReplacementAsync(
        Guid enrollmentId,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!_enrollments.TryGetValue(enrollmentId, out var enrollment) ||
                !enrollment.IsActive ||
                enrollment.PendingReplacement is null)
            {
                return Task.FromResult(false);
            }

            _enrollments[enrollmentId] = enrollment with
            {
                Secret = enrollment.PendingReplacement.Secret,
                Digits = enrollment.PendingReplacement.Digits,
                PeriodSeconds = enrollment.PendingReplacement.PeriodSeconds,
                Algorithm = enrollment.PendingReplacement.Algorithm,
                ConfirmedUtc = confirmedAt,
                RevokedUtc = null,
                PendingReplacement = null,
            };

            return Task.FromResult(true);
        }
    }

    public Task<bool> RevokeAsync(
        Guid enrollmentId,
        DateTimeOffset revokedAt,
        FactorRevocationSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!_enrollments.TryGetValue(enrollmentId, out var enrollment) || !enrollment.IsActive)
            {
                return Task.FromResult(false);
            }

            _enrollments[enrollmentId] = enrollment with
            {
                IsActive = false,
                RevokedUtc = revokedAt,
                PendingReplacement = null,
            };

            if (sideEffects?.WebhookEvent is not null)
            {
                foreach (var subscription in _subscriptions.Values.Where(subscription =>
                             subscription.IsActive &&
                             subscription.TenantId == sideEffects.WebhookEvent.TenantId &&
                             subscription.ApplicationClientId == sideEffects.WebhookEvent.ApplicationClientId &&
                             subscription.EventTypes.Contains(sideEffects.WebhookEvent.EventType, StringComparer.Ordinal)))
                {
                    var deliveryId = Guid.NewGuid();
                    _webhookDeliveries[deliveryId] = new WebhookEventDelivery
                    {
                        DeliveryId = deliveryId,
                        SubscriptionId = subscription.SubscriptionId,
                        TenantId = subscription.TenantId,
                        ApplicationClientId = subscription.ApplicationClientId,
                        EndpointUrl = subscription.EndpointUrl,
                        EventId = sideEffects.WebhookEvent.EventId,
                        EventType = sideEffects.WebhookEvent.EventType,
                        OccurredAtUtc = sideEffects.WebhookEvent.OccurredAtUtc,
                        ResourceType = sideEffects.WebhookEvent.ResourceType,
                        ResourceId = sideEffects.WebhookEvent.ResourceId,
                        PayloadJson = sideEffects.WebhookEvent.PayloadJson,
                        Status = WebhookEventDeliveryStatus.Queued,
                        AttemptCount = 0,
                        NextAttemptUtc = sideEffects.WebhookEvent.OccurredAtUtc,
                        CreatedUtc = sideEffects.WebhookEvent.OccurredAtUtc,
                    };
                }
            }

            return Task.FromResult(true);
        }
    }

    public Task IncrementFailedConfirmationAttemptsAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            var enrollment = _enrollments[enrollmentId];
            _enrollments[enrollmentId] = enrollment with
            {
                FailedConfirmationAttempts = enrollment.FailedConfirmationAttempts + 1,
            };
        }

        return Task.CompletedTask;
    }

    public Task IncrementFailedReplacementConfirmationAttemptsAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            var enrollment = _enrollments[enrollmentId];
            if (enrollment.PendingReplacement is null)
            {
                return Task.CompletedTask;
            }

            _enrollments[enrollmentId] = enrollment with
            {
                PendingReplacement = enrollment.PendingReplacement with
                {
                    FailedConfirmationAttempts = enrollment.PendingReplacement.FailedConfirmationAttempts + 1,
                },
            };
        }

        return Task.CompletedTask;
    }

    private static TotpEnrollmentProvisioningRecord CreateEnrollment(
        string externalUserId,
        bool isActive,
        DateTimeOffset? confirmedUtc,
        int failedConfirmationAttempts,
        TotpPendingReplacementRecord? pendingReplacement,
        Guid? applicationClientId = null)
    {
        return new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = Guid.NewGuid(),
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = applicationClientId ?? EnrollmentApiTestContext.ApplicationClientId,
            ExternalUserId = externalUserId,
            Label = externalUserId,
            Secret = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
            IsActive = isActive,
            ConfirmedUtc = confirmedUtc,
            RevokedUtc = isActive ? null : DateTimeOffset.UtcNow,
            FailedConfirmationAttempts = failedConfirmationAttempts,
            PendingReplacement = pendingReplacement,
        };
    }

    private void Store(TotpEnrollmentProvisioningRecord enrollment)
    {
        lock (_syncRoot)
        {
            _enrollments[enrollment.EnrollmentId] = enrollment;
        }
    }

    public void SeedWebhookSubscription(WebhookSubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions[subscription.SubscriptionId] = subscription;
        }
    }

    public IReadOnlyCollection<WebhookEventDelivery> GetWebhookDeliveries()
    {
        lock (_syncRoot)
        {
            return _webhookDeliveries.Values
                .OrderBy(delivery => delivery.CreatedUtc)
                .ToArray();
        }
    }
}
