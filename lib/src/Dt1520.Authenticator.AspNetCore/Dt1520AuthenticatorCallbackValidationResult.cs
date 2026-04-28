using Dt1520.Authenticator.Client;
using Microsoft.AspNetCore.Http;

namespace Dt1520.Authenticator.AspNetCore;

/// <summary>
/// Sanitized result returned by ASP.NET Core callback validation helpers.
/// </summary>
public sealed record Dt1520AuthenticatorCallbackValidationResult
{
    private Dt1520AuthenticatorCallbackValidationResult(
        bool isValid,
        Dt1520AuthenticatorCallbackValidationFailureKind? failureKind,
        int? bodyLength,
        int recommendedStatusCode)
    {
        IsValid = isValid;
        FailureKind = failureKind;
        BodyLength = bodyLength;
        RecommendedStatusCode = recommendedStatusCode;
    }

    /// <summary>
    /// Indicates whether the callback signature is valid for the original request body bytes.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Stable failure reason when <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public Dt1520AuthenticatorCallbackValidationFailureKind? FailureKind { get; }

    /// <summary>
    /// Number of callback body bytes that were validated. The body content is never stored in this result.
    /// </summary>
    public int? BodyLength { get; }

    /// <summary>
    /// Safe HTTP status code to use for a failed callback validation response.
    /// </summary>
    public int RecommendedStatusCode { get; }

    /// <summary>
    /// Creates a successful callback validation result.
    /// </summary>
    public static Dt1520AuthenticatorCallbackValidationResult Valid(int bodyLength)
    {
        return new Dt1520AuthenticatorCallbackValidationResult(
            isValid: true,
            failureKind: null,
            bodyLength: bodyLength,
            recommendedStatusCode: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Creates a failed callback validation result.
    /// </summary>
    public static Dt1520AuthenticatorCallbackValidationResult Failed(
        Dt1520AuthenticatorCallbackValidationFailureKind failureKind)
    {
        return new Dt1520AuthenticatorCallbackValidationResult(
            isValid: false,
            failureKind: failureKind,
            bodyLength: null,
            recommendedStatusCode: failureKind == Dt1520AuthenticatorCallbackValidationFailureKind.BodyTooLarge
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Converts a failed validation result into a sanitized ASP.NET Core <see cref="IResult"/>.
    /// </summary>
    public IResult ToFailureHttpResult()
    {
        if (IsValid)
        {
            throw new InvalidOperationException("A valid callback result does not have a failure response.");
        }

        return new CallbackValidationFailureHttpResult(RecommendedStatusCode);
    }

    internal static Dt1520AuthenticatorCallbackValidationResult FromSignatureResult(
        CallbackSignatureVerificationResult result,
        int bodyLength)
    {
        if (result.IsValid)
        {
            return Valid(bodyLength);
        }

        return Failed(result.FailureKind switch
        {
            CallbackSignatureVerificationFailureKind.MissingSignature => Dt1520AuthenticatorCallbackValidationFailureKind.MissingSignature,
            CallbackSignatureVerificationFailureKind.InvalidFormat => Dt1520AuthenticatorCallbackValidationFailureKind.InvalidFormat,
            CallbackSignatureVerificationFailureKind.UnsupportedAlgorithm => Dt1520AuthenticatorCallbackValidationFailureKind.UnsupportedAlgorithm,
            CallbackSignatureVerificationFailureKind.TimestampOutsideTolerance => Dt1520AuthenticatorCallbackValidationFailureKind.TimestampOutsideTolerance,
            CallbackSignatureVerificationFailureKind.SignatureMismatch => Dt1520AuthenticatorCallbackValidationFailureKind.SignatureMismatch,
            _ => Dt1520AuthenticatorCallbackValidationFailureKind.InvalidFormat,
        });
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(Dt1520AuthenticatorCallbackValidationResult)} {{ {nameof(IsValid)} = {IsValid}, {nameof(FailureKind)} = {FailureKind}, {nameof(BodyLength)} = {BodyLength}, {nameof(RecommendedStatusCode)} = {RecommendedStatusCode} }}";
    }

    private sealed class CallbackValidationFailureHttpResult(int statusCode) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";
            return httpContext.Response.WriteAsJsonAsync(new
            {
                title = "DT-1520 callback validation failed.",
                detail = "The callback signature or request body could not be validated.",
                status = statusCode,
            });
        }
    }
}
