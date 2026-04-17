using OtpAuth.Application.Administration;
using OtpAuth.Application.Enrollments;

namespace OtpAuth.Api.Admin;

public static class AdminTotpEnrollmentCommandRequestMapper
{
    public static AdminStartTotpEnrollmentRequest Map(AdminStartTotpEnrollmentHttpRequest request)
    {
        return new AdminStartTotpEnrollmentRequest
        {
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            ExternalUserId = request.ExternalUserId,
            Issuer = request.Issuer,
            Label = request.Label,
        };
    }

    public static ConfirmTotpEnrollmentRequest Map(Guid enrollmentId, AdminConfirmTotpEnrollmentHttpRequest request)
    {
        return new ConfirmTotpEnrollmentRequest
        {
            EnrollmentId = enrollmentId,
            Code = request.Code,
        };
    }

    public static AdminTotpEnrollmentCommandHttpResponse MapResponse(TotpEnrollmentView enrollment)
    {
        return new AdminTotpEnrollmentCommandHttpResponse
        {
            EnrollmentId = enrollment.EnrollmentId,
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
            SecretUri = enrollment.SecretUri,
            QrCodePayload = enrollment.QrCodePayload,
        };
    }
}
