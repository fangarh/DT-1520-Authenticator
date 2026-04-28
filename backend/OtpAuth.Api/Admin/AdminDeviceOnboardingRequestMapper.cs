using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Api.Admin;

public static class AdminDeviceOnboardingRequestMapper
{
    public static AdminDeviceOnboardingCreateRequest Map(AdminCreateDeviceOnboardingArtifactHttpRequest request)
    {
        return new AdminDeviceOnboardingCreateRequest
        {
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            ExternalUserId = request.ExternalUserId ?? string.Empty,
            Platform = ParsePlatform(request.Platform),
            TtlMinutes = request.TtlMinutes ?? AdminDeviceOnboardingValidation.DefaultTtlMinutes,
            HasOperatorProvidedActivationPayload = !string.IsNullOrWhiteSpace(request.ActivationPayload),
        };
    }

    public static AdminDeviceOnboardingArtifactHttpResponse MapResponse(AdminDeviceOnboardingView artifact)
    {
        return new AdminDeviceOnboardingArtifactHttpResponse
        {
            ActivationCodeId = artifact.ActivationCodeId,
            TenantId = artifact.TenantId,
            ApplicationClientId = artifact.ApplicationClientId,
            ExternalUserId = artifact.ExternalUserId,
            Platform = MapPlatform(artifact.Platform),
            Status = artifact.Status switch
            {
                AdminDeviceOnboardingStatus.Pending => "pending",
                AdminDeviceOnboardingStatus.Consumed => "consumed",
                AdminDeviceOnboardingStatus.Expired => "expired",
                AdminDeviceOnboardingStatus.Revoked => "revoked",
                _ => "unknown",
            },
            ExpiresAtUtc = artifact.ExpiresUtc,
            ConsumedAtUtc = artifact.ConsumedUtc,
            RevokedAtUtc = artifact.RevokedUtc,
            CreatedAtUtc = artifact.CreatedUtc,
        };
    }

    public static AdminCreateDeviceOnboardingArtifactHttpResponse MapCreateResponse(
        AdminDeviceOnboardingView artifact,
        string activationPayload)
    {
        return new AdminCreateDeviceOnboardingArtifactHttpResponse
        {
            Artifact = MapResponse(artifact),
            ActivationPayload = activationPayload,
        };
    }

    public static AdminDeviceOnboardingStatus? ParseStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "pending" => AdminDeviceOnboardingStatus.Pending,
            "consumed" => AdminDeviceOnboardingStatus.Consumed,
            "expired" => AdminDeviceOnboardingStatus.Expired,
            "revoked" => AdminDeviceOnboardingStatus.Revoked,
            _ => null,
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

    private static string MapPlatform(DevicePlatform platform)
    {
        return platform switch
        {
            DevicePlatform.Android => "android",
            DevicePlatform.Ios => "ios",
            _ => "unknown",
        };
    }
}
