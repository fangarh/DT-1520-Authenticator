namespace Dt1520.Authenticator.Client;

internal static class ChallengeRequestValidator
{
    public static Dt1520AuthenticatorError? ValidateCreate(CreateChallengeRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return ValidationFailure("TenantId is required.");
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            return ValidationFailure("ApplicationClientId is required.");
        }

        if (request.Subject is null || string.IsNullOrWhiteSpace(request.Subject.ExternalUserId))
        {
            return ValidationFailure("Subject.ExternalUserId is required.");
        }

        if (request.Operation is null)
        {
            return ValidationFailure("Operation is required.");
        }

        if (request.Callback?.Url is { } callbackUrl && (!callbackUrl.IsAbsoluteUri || !IsHttpScheme(callbackUrl)))
        {
            return ValidationFailure("Callback.Url must be an absolute http or https URL.");
        }

        if (request.TargetDeviceId == Guid.Empty)
        {
            return ValidationFailure("TargetDeviceId must not be empty.");
        }

        if (request.PreferredFactors is { Count: 0 })
        {
            return ValidationFailure("PreferredFactors must be omitted or contain at least one factor.");
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && HasInvalidHeaderValue(request.IdempotencyKey))
        {
            return ValidationFailure("IdempotencyKey contains invalid HTTP header characters.");
        }

        return null;
    }

    public static Dt1520AuthenticatorError? ValidateChallengeId(Guid challengeId)
    {
        return challengeId == Guid.Empty ? ValidationFailure("ChallengeId is required.") : null;
    }

    public static Dt1520AuthenticatorError? ValidateVerifyTotp(Guid challengeId, VerifyTotpRequest? request)
    {
        var challengeError = ValidateChallengeId(challengeId);
        if (challengeError is not null)
        {
            return challengeError;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Code is required.");
        }

        if (request.Code.Length != 6 || request.Code.Any(static value => value < '0' || value > '9'))
        {
            return ValidationFailure("Code must contain exactly six digits.");
        }

        return null;
    }

    private static Dt1520AuthenticatorError ValidationFailure(string title)
    {
        return ProblemDetailsMapper.CreateValidationFailure(title);
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasInvalidHeaderValue(string value)
    {
        return value.Any(static character => character is '\r' or '\n');
    }
}
