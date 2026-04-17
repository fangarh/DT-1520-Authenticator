using OtpAuth.Application.Enrollments;

namespace OtpAuth.Api.Admin;

public static class AdminTotpEnrollmentRequestMapper
{
    public static AdminTotpEnrollmentCurrentHttpResponse MapResponse(TotpEnrollmentAdminView enrollment)
    {
        return new AdminTotpEnrollmentCurrentHttpResponse
        {
            EnrollmentId = enrollment.EnrollmentId,
            TenantId = enrollment.TenantId,
            ApplicationClientId = enrollment.ApplicationClientId,
            ExternalUserId = enrollment.ExternalUserId,
            Label = enrollment.Label,
            Status = enrollment.Status switch
            {
                TotpEnrollmentStatus.Pending => "pending",
                TotpEnrollmentStatus.Confirmed => "confirmed",
                TotpEnrollmentStatus.Revoked => "revoked",
                _ => "unknown",
            },
            HasPendingReplacement = enrollment.HasPendingReplacement,
            ConfirmedAtUtc = enrollment.ConfirmedAtUtc,
            RevokedAtUtc = enrollment.RevokedAtUtc,
        };
    }
}
