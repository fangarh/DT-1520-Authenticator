using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Api.Admin;

public static class AdminCombinedOnboardingRequestMapper
{
    public static AdminCreateCombinedOnboardingPackageRequest Map(
        AdminCreateCombinedOnboardingPackageHttpRequest request)
    {
        return new AdminCreateCombinedOnboardingPackageRequest
        {
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            ExternalUserId = request.ExternalUserId ?? string.Empty,
            Platform = ParsePlatform(request.Platform),
            TtlMinutes = request.TtlMinutes ?? AdminDeviceOnboardingValidation.DefaultTtlMinutes,
            Issuer = request.Issuer,
            Label = request.Label,
        };
    }

    public static AdminCreateCombinedOnboardingPackageHttpResponse MapResponse(
        AdminDeviceOnboardingView deviceArtifact,
        string activationPayload,
        OtpAuth.Application.Enrollments.TotpEnrollmentView totpEnrollment)
    {
        return new AdminCreateCombinedOnboardingPackageHttpResponse
        {
            DeviceArtifact = AdminDeviceOnboardingRequestMapper.MapResponse(deviceArtifact),
            ActivationPayload = activationPayload,
            TotpEnrollment = AdminTotpEnrollmentCommandRequestMapper.MapResponse(totpEnrollment),
        };
    }

    private static DevicePlatform ParsePlatform(string? platform)
    {
        return platform?.Trim().ToLowerInvariant() switch
        {
            "android" => DevicePlatform.Android,
            "ios" => DevicePlatform.Ios,
            _ => DevicePlatform.Unknown,
        };
    }
}
