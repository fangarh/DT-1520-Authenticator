using Dt1520.Authenticator.AspNetCore;
using Dt1520.Authenticator.Client;
using Dt1520.Authenticator.Desktop;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed class ProtectedOperationCoordinator
{
    private readonly IReferenceAuthenticatorGateway _authenticator;
    private readonly Dt1520AuthenticatorCallbackValidator _callbackValidator;
    private readonly ReferenceBackendOptions _options;
    private readonly IProtectedOperationStore _store;
    private readonly TimeProvider _timeProvider;

    public ProtectedOperationCoordinator(
        IReferenceAuthenticatorGateway authenticator,
        Dt1520AuthenticatorCallbackValidator callbackValidator,
        IOptions<ReferenceBackendOptions> options,
        IProtectedOperationStore store,
        TimeProvider timeProvider)
    {
        _authenticator = authenticator;
        _callbackValidator = callbackValidator;
        _options = options.Value;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<ReferenceBackendResult<ReferenceApprovalSession>> StartAsync(
        StartProtectedOperationRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateStart(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var utcNow = _timeProvider.GetUtcNow();
        var record = await _store.CreatePendingAsync(
            request,
            utcNow,
            utcNow.Add(_options.DefaultOperationTtl),
            cancellationToken);

        var requestedAtUtc = _timeProvider.GetUtcNow();
        var challenge = await _authenticator.CreateChallengeAsync(record, cancellationToken);
        if (!challenge.IsSuccess || challenge.Value is null)
        {
            await _store.MarkFailedAsync(record.SessionId, "dt1520_create_failed", _timeProvider.GetUtcNow(), cancellationToken);
            return FromSdkFailure<ReferenceApprovalSession>(challenge.Error);
        }

        var createdAtUtc = _timeProvider.GetUtcNow();
        var updated = await _store.BindChallengeAsync(
            record.SessionId,
            challenge.Value,
            requestedAtUtc,
            createdAtUtc,
            cancellationToken);

        return ReferenceBackendResult<ReferenceApprovalSession>.Success(updated!.ToSession());
    }

    public async Task<ReferenceApprovalSession?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var record = await _store.GetAsync(sessionId, cancellationToken);
        return record?.ToSession();
    }

    public async Task<ReferenceBackendResult<ReferenceApprovalSession>> VerifyTotpAsync(
        string sessionId,
        VerifyTotpFallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure(
                "TOTP code is required.",
                StatusCodes.Status400BadRequest);
        }

        var record = await _store.GetAsync(sessionId, cancellationToken);
        if (record?.ChallengeId is null)
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure(
                "Approval session was not found.",
                StatusCodes.Status404NotFound);
        }

        var submittedAtUtc = _timeProvider.GetUtcNow();
        var result = await _authenticator.VerifyTotpAsync(record.ChallengeId.Value, request.Code, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return FromSdkFailure<ReferenceApprovalSession>(result.Error);
        }

        var updated = await _store.ApplyChallengeStatusAsync(
            sessionId,
            result.Value.Id,
            result.Value.Status,
            _timeProvider.GetUtcNow(),
            submittedAtUtc,
            cancellationToken);

        return updated is null
            ? ReferenceBackendResult<ReferenceApprovalSession>.Failure("Approval session was not found.", StatusCodes.Status404NotFound)
            : ReferenceBackendResult<ReferenceApprovalSession>.Success(updated.ToSession());
    }

    public async Task<ReferenceBackendResult<object>> ApplyCallbackAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _callbackValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ReferenceBackendResult<object>.Failure(
                "DT-1520 callback signature is invalid.",
                StatusCodes.Status401Unauthorized,
                validation.FailureKind?.ToString());
        }

        var envelope = await request.ReadFromJsonAsync<ChallengeCallbackEnvelope>(
            cancellationToken: cancellationToken);
        if (envelope?.Challenge.CorrelationId is null)
        {
            return ReferenceBackendResult<object>.Failure("Callback payload is invalid.", StatusCodes.Status400BadRequest);
        }

        var updated = await _store.ApplyChallengeStatusAsync(
            envelope.Challenge.CorrelationId,
            envelope.Challenge.Id,
            envelope.Challenge.Status,
            _timeProvider.GetUtcNow(),
            totpSubmittedAtUtc: null,
            cancellationToken);

        return updated is null
            ? ReferenceBackendResult<object>.Failure("Approval session was not found.", StatusCodes.Status404NotFound)
            : ReferenceBackendResult<object>.Success(new object());
    }

    private static ReferenceBackendResult<ReferenceApprovalSession>? ValidateStart(
        StartProtectedOperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure(
                "External user id is required.",
                StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return ReferenceBackendResult<ReferenceApprovalSession>.Failure(
                "Operation display name is required.",
                StatusCodes.Status400BadRequest);
        }

        return null;
    }

    private static ReferenceBackendResult<T> FromSdkFailure<T>(Dt1520AuthenticatorError? error)
    {
        return ReferenceBackendResult<T>.Failure(
            error?.Title ?? "DT-1520 request failed.",
            error?.StatusCode ?? StatusCodes.Status502BadGateway,
            error?.Detail);
    }
}
