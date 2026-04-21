using System.Text.RegularExpressions;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed partial class VerifyTotpHandler
{
    private readonly IChallengeRepository _challengeRepository;
    private readonly IChallengeAttemptRecorder _challengeAttemptRecorder;
    private readonly ITotpVerificationRateLimiter _totpVerificationRateLimiter;
    private readonly ITotpVerifier _totpVerifier;

    public VerifyTotpHandler(
        IChallengeRepository challengeRepository,
        IChallengeAttemptRecorder challengeAttemptRecorder,
        ITotpVerificationRateLimiter totpVerificationRateLimiter,
        ITotpVerifier totpVerifier)
    {
        _challengeRepository = challengeRepository;
        _challengeAttemptRecorder = challengeAttemptRecorder;
        _totpVerificationRateLimiter = totpVerificationRateLimiter;
        _totpVerifier = totpVerifier;
    }

    public async Task<VerifyTotpResult> HandleAsync(
        VerifyTotpRequest request,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return VerifyTotpResult.Failure(VerifyTotpErrorCode.ValidationFailed, validationError);
        }

        if (!clientContext.HasScope(IntegrationClientScopes.ChallengesWrite))
        {
            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.AccessDenied,
                $"Scope '{IntegrationClientScopes.ChallengesWrite}' is required.");
        }

        var challenge = await _challengeRepository.GetByIdAsync(
            request.ChallengeId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
        if (challenge is null)
        {
            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.NotFound,
                $"Challenge '{request.ChallengeId}' was not found.");
        }

        if (challenge.FactorType != FactorType.Totp)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptResults.UnsupportedFactor, cancellationToken);
            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' does not support TOTP verification.",
                challenge);
        }

        if (challenge.Status != ChallengeStatus.Pending)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptResults.InvalidState, cancellationToken);
            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' is not pending.",
                challenge);
        }

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expiredAtUtc = DateTimeOffset.UtcNow;
            var expiredChallenge = challenge.MarkExpired();
            await _challengeRepository.UpdateAsync(
                expiredChallenge,
                ChallengeUpdateSideEffects.CreateForTerminalState(expiredChallenge, expiredAtUtc),
                cancellationToken);
            await RecordAttemptAsync(expiredChallenge.Id, ChallengeAttemptResults.Expired, cancellationToken);

            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.ChallengeExpired,
                "Challenge has expired.",
                expiredChallenge);
        }

        var now = DateTimeOffset.UtcNow;
        var rateLimitDecision = await _totpVerificationRateLimiter.EvaluateAsync(
            challenge,
            now,
            cancellationToken);
        if (!rateLimitDecision.IsAllowed)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptResults.RateLimited, cancellationToken);

            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.RateLimited,
                "Too many TOTP verification attempts. Retry later.",
                challenge,
                rateLimitDecision.RetryAfterSeconds);
        }

        var verificationResult = await _totpVerifier.VerifyAsync(
            challenge,
            request.Code.Trim(),
            now,
            cancellationToken);

        if (verificationResult.Status == TotpVerificationStatus.ReplayDetected)
        {
            var failedChallenge = challenge.MarkFailed();
            await _challengeRepository.UpdateAsync(failedChallenge, cancellationToken);
            await RecordAttemptAsync(failedChallenge.Id, ChallengeAttemptResults.ReplayDetected, cancellationToken);

            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.InvalidCode,
                "Invalid one-time password.",
                failedChallenge);
        }

        if (verificationResult.Status != TotpVerificationStatus.Valid)
        {
            var failedChallenge = challenge.MarkFailed();
            await _challengeRepository.UpdateAsync(failedChallenge, cancellationToken);
            await RecordAttemptAsync(failedChallenge.Id, ChallengeAttemptResults.InvalidCode, cancellationToken);

            return VerifyTotpResult.Failure(
                VerifyTotpErrorCode.InvalidCode,
                "Invalid one-time password.",
                failedChallenge);
        }

        var approvedAtUtc = DateTimeOffset.UtcNow;
        var approvedChallenge = challenge.MarkApproved(approvedAtUtc);
        await _challengeRepository.UpdateAsync(
            approvedChallenge,
            ChallengeUpdateSideEffects.CreateForTerminalState(approvedChallenge, approvedAtUtc),
            cancellationToken);
        await RecordAttemptAsync(approvedChallenge.Id, ChallengeAttemptResults.Approved, cancellationToken);

        return VerifyTotpResult.Success(approvedChallenge);
    }

    private Task RecordAttemptAsync(Guid challengeId, string result, CancellationToken cancellationToken)
    {
        return _challengeAttemptRecorder.RecordAsync(
            new ChallengeAttemptRecord
            {
                ChallengeId = challengeId,
                AttemptType = ChallengeAttemptTypes.TotpVerify,
                Result = result,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static string? Validate(VerifyTotpRequest request)
    {
        if (request.ChallengeId == Guid.Empty)
        {
            return "ChallengeId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Code is required.";
        }

        if (!TotpCodePattern().IsMatch(request.Code.Trim()))
        {
            return "Code must be a 6-digit numeric value.";
        }

        return null;
    }

    [GeneratedRegex("^\\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex TotpCodePattern();
}
