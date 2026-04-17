using OtpAuth.Application.Enrollments;

namespace OtpAuth.Api.Enrollments;

public static class TotpEnrollmentRequestMapper
{
    public static StartTotpEnrollmentRequest Map(StartTotpEnrollmentHttpRequest request)
    {
        return new StartTotpEnrollmentRequest
        {
            TenantId = request.TenantId,
            ExternalUserId = request.ExternalUserId,
            Issuer = request.Issuer,
            Label = request.Label,
        };
    }

    public static ConfirmTotpEnrollmentRequest Map(
        Guid enrollmentId,
        ConfirmTotpEnrollmentHttpRequest request)
    {
        return new ConfirmTotpEnrollmentRequest
        {
            EnrollmentId = enrollmentId,
            Code = request.Code,
        };
    }

    public static TotpEnrollmentHttpResponse MapResponse(TotpEnrollmentView enrollment)
    {
        return new TotpEnrollmentHttpResponse
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
            SecretUri = enrollment.SecretUri,
            QrCodePayload = enrollment.QrCodePayload,
        };
    }
}
