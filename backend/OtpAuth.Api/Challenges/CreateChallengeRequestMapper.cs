using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Api.Challenges;

public static class CreateChallengeRequestMapper
{
    public static bool TryMap(
        CreateChallengeHttpRequest httpRequest,
        out CreateChallengeRequest? request,
        out string? validationError)
    {
        request = null;
        validationError = null;

        if (httpRequest.Subject is null)
        {
            validationError = "Subject is required.";
            return false;
        }

        if (httpRequest.Operation is null)
        {
            validationError = "Operation is required.";
            return false;
        }

        if (!TryMapOperationType(httpRequest.Operation.Type, out var operationType))
        {
            validationError = $"Unsupported operation type '{httpRequest.Operation.Type}'.";
            return false;
        }

        var preferredFactors = new List<FactorType>();
        if (httpRequest.PreferredFactors is not null)
        {
            foreach (var factor in httpRequest.PreferredFactors)
            {
                if (!TryMapFactorType(factor, out var factorType))
                {
                    validationError = $"Unsupported preferred factor '{factor}'.";
                    return false;
                }

                preferredFactors.Add(factorType);
            }
        }

        Uri? callbackUrl = null;
        if (httpRequest.Callback is not null)
        {
            if (!Uri.TryCreate(httpRequest.Callback.Url, UriKind.Absolute, out callbackUrl))
            {
                validationError = "Callback URL must be an absolute URI.";
                return false;
            }
        }

        request = new CreateChallengeRequest
        {
            TenantId = httpRequest.TenantId,
            ApplicationClientId = httpRequest.ApplicationClientId,
            ExternalUserId = httpRequest.Subject.ExternalUserId,
            Username = httpRequest.Subject.Username,
            OperationType = operationType,
            OperationDisplayName = httpRequest.Operation.DisplayName,
            PreferredFactors = preferredFactors,
            TargetDeviceId = httpRequest.TargetDeviceId,
            CorrelationId = httpRequest.CorrelationId,
            CallbackUrl = callbackUrl,
        };

        return true;
    }

    public static ChallengeHttpResponse MapResponse(Domain.Challenges.Challenge challenge)
    {
        return new ChallengeHttpResponse
        {
            Id = challenge.Id,
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            FactorType = MapFactorType(challenge.FactorType),
            Status = MapStatus(challenge.Status),
            ExpiresAt = challenge.ExpiresAt,
            TargetDeviceId = challenge.TargetDeviceId,
            ApprovedAt = challenge.ApprovedUtc,
            DeniedAt = challenge.DeniedUtc,
            CorrelationId = challenge.CorrelationId,
        };
    }

    private static bool TryMapOperationType(string? rawValue, out OperationType operationType)
    {
        operationType = rawValue?.Trim().ToLowerInvariant() switch
        {
            "login" => OperationType.Login,
            "step_up" => OperationType.StepUp,
            "backup_code_recovery" => OperationType.BackupCodeRecovery,
            _ => OperationType.Unknown,
        };

        return operationType != OperationType.Unknown;
    }

    private static bool TryMapFactorType(string? rawValue, out FactorType factorType)
    {
        factorType = rawValue?.Trim().ToLowerInvariant() switch
        {
            "totp" => FactorType.Totp,
            "push" => FactorType.Push,
            "backup_code" => FactorType.BackupCode,
            _ => FactorType.Unknown,
        };

        return factorType != FactorType.Unknown;
    }

    private static string MapFactorType(FactorType factorType)
    {
        return factorType switch
        {
            FactorType.Totp => "totp",
            FactorType.Push => "push",
            FactorType.BackupCode => "backup_code",
            _ => "unknown",
        };
    }

    private static string MapStatus(Domain.Challenges.ChallengeStatus status)
    {
        return status switch
        {
            Domain.Challenges.ChallengeStatus.Pending => "pending",
            Domain.Challenges.ChallengeStatus.Approved => "approved",
            Domain.Challenges.ChallengeStatus.Denied => "denied",
            Domain.Challenges.ChallengeStatus.Expired => "expired",
            Domain.Challenges.ChallengeStatus.Failed => "failed",
            _ => "unknown",
        };
    }
}
