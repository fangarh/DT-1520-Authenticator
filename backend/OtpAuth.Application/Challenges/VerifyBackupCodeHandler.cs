using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed class VerifyBackupCodeHandler
{
    private readonly IBackupCodeVerificationRateLimiter _backupCodeVerificationRateLimiter;
    private readonly IBackupCodeVerifier _backupCodeVerifier;
    private readonly IChallengeAttemptRecorder _challengeAttemptRecorder;
    private readonly IChallengeRepository _challengeRepository;

    public VerifyBackupCodeHandler(
        IChallengeRepository challengeRepository,
        IChallengeAttemptRecorder challengeAttemptRecorder,
        IBackupCodeVerificationRateLimiter backupCodeVerificationRateLimiter,
        IBackupCodeVerifier backupCodeVerifier)
    {
        _challengeRepository = challengeRepository;
        _challengeAttemptRecorder = challengeAttemptRecorder;
        _backupCodeVerificationRateLimiter = backupCodeVerificationRateLimiter;
        _backupCodeVerifier = backupCodeVerifier;
    }

    public async Task<VerifyBackupCodeResult> HandleAsync(
        VerifyBackupCodeRequest request,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return VerifyBackupCodeResult.Failure(VerifyBackupCodeErrorCode.ValidationFailed, validationError);
        }

        if (!clientContext.HasScope(IntegrationClientScopes.ChallengesWrite))
        {
            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.AccessDenied,
                $"Scope '{IntegrationClientScopes.ChallengesWrite}' is required.");
        }

        var challenge = await _challengeRepository.GetByIdAsync(
            request.ChallengeId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
        if (challenge is null)
        {
            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.NotFound,
                $"Challenge '{request.ChallengeId}' was not found.");
        }

        if (challenge.FactorType != FactorType.BackupCode)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptResults.UnsupportedFactor, cancellationToken);
            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' does not support backup code verification.",
                challenge);
        }

        if (challenge.Status != ChallengeStatus.Pending)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptResults.InvalidState, cancellationToken);
            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' is not pending.",
                challenge);
        }

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expiredChallenge = challenge.MarkExpired();
            await _challengeRepository.UpdateAsync(expiredChallenge, cancellationToken);
            await RecordAttemptAsync(expiredChallenge.Id, ChallengeAttemptResults.Expired, cancellationToken);

            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.ChallengeExpired,
                "Challenge has expired.",
                expiredChallenge);
        }

        var now = DateTimeOffset.UtcNow;
        var rateLimitDecision = await _backupCodeVerificationRateLimiter.EvaluateAsync(
            challenge,
            now,
            cancellationToken);
        if (!rateLimitDecision.IsAllowed)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptResults.RateLimited, cancellationToken);

            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.RateLimited,
                "Too many backup code verification attempts. Retry later.",
                challenge,
                rateLimitDecision.RetryAfterSeconds);
        }

        var verificationResult = await _backupCodeVerifier.VerifyAsync(
            challenge,
            request.Code,
            now,
            cancellationToken);
        if (verificationResult.Status != BackupCodeVerificationStatus.Valid)
        {
            var failedChallenge = challenge.MarkFailed();
            await _challengeRepository.UpdateAsync(failedChallenge, cancellationToken);
            await RecordAttemptAsync(failedChallenge.Id, ChallengeAttemptResults.InvalidCode, cancellationToken);

            return VerifyBackupCodeResult.Failure(
                VerifyBackupCodeErrorCode.InvalidCode,
                "Invalid backup code.",
                failedChallenge);
        }

        var approvedChallenge = challenge.MarkApproved();
        await _challengeRepository.UpdateAsync(approvedChallenge, cancellationToken);
        await RecordAttemptAsync(approvedChallenge.Id, ChallengeAttemptResults.Approved, cancellationToken);

        return VerifyBackupCodeResult.Success(approvedChallenge);
    }

    private Task RecordAttemptAsync(Guid challengeId, string result, CancellationToken cancellationToken)
    {
        return _challengeAttemptRecorder.RecordAsync(
            new ChallengeAttemptRecord
            {
                ChallengeId = challengeId,
                AttemptType = ChallengeAttemptTypes.BackupCodeVerify,
                Result = result,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static string? Validate(VerifyBackupCodeRequest request)
    {
        if (request.ChallengeId == Guid.Empty)
        {
            return "ChallengeId is required.";
        }

        return BackupCodeFormat.TryNormalize(request.Code, out _, out var validationError)
            ? null
            : validationError;
    }
}
