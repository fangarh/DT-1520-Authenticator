using Dt1520.Authenticator.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.AspNetCore;

/// <summary>
/// ASP.NET Core helper that validates DT-1520 callback signatures over original request body bytes.
/// </summary>
public sealed class Dt1520AuthenticatorCallbackValidator
{
    private const int BufferThresholdBytes = 30 * 1024;
    private readonly ILogger<Dt1520AuthenticatorCallbackValidator>? _logger;
    private readonly IOptionsMonitor<Dt1520AuthenticatorAspNetCoreOptions> _options;

    /// <summary>
    /// Creates a callback validator.
    /// </summary>
    public Dt1520AuthenticatorCallbackValidator(
        IOptionsMonitor<Dt1520AuthenticatorAspNetCoreOptions> options,
        ILogger<Dt1520AuthenticatorCallbackValidator>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Validates the current request using <c>X-OTPAuth-Signature</c> and the original request body bytes.
    /// </summary>
    /// <remarks>
    /// The request body is buffered before reading and reset to position zero after validation so controllers
    /// and minimal API handlers can parse the body after signature validation. The raw body is not stored in
    /// the returned result and is not logged by this helper.
    /// </remarks>
    public async Task<Dt1520AuthenticatorCallbackValidationResult> ValidateAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.CurrentValue;
        if (request.ContentLength > options.MaxCallbackBodyBytes)
        {
            LogValidationFailure(Dt1520AuthenticatorCallbackValidationFailureKind.BodyTooLarge);
            return Dt1520AuthenticatorCallbackValidationResult.Failed(
                Dt1520AuthenticatorCallbackValidationFailureKind.BodyTooLarge);
        }

        byte[] bodyBytes;
        try
        {
            request.EnableBuffering(BufferThresholdBytes, options.MaxCallbackBodyBytes);
            bodyBytes = await ReadRequestBodyAsync(request, options.MaxCallbackBodyBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            LogValidationFailure(Dt1520AuthenticatorCallbackValidationFailureKind.BodyReadFailed);
            return Dt1520AuthenticatorCallbackValidationResult.Failed(
                Dt1520AuthenticatorCallbackValidationFailureKind.BodyReadFailed);
        }
        catch (InvalidDataException)
        {
            LogValidationFailure(Dt1520AuthenticatorCallbackValidationFailureKind.BodyTooLarge);
            return Dt1520AuthenticatorCallbackValidationResult.Failed(
                Dt1520AuthenticatorCallbackValidationFailureKind.BodyTooLarge);
        }
        finally
        {
            ResetBodyPosition(request);
        }

        var verifier = new CallbackSignatureVerifier(options.ToCallbackVerifierOptions());
        var signatureHeaderValue = request.Headers[CallbackSignatureVerifier.SignatureHeaderName].FirstOrDefault();
        var timestampHeaderValue = request.Headers[CallbackSignatureVerifier.TimestampHeaderName].FirstOrDefault();
        var signatureResult = verifier.Verify(bodyBytes, signatureHeaderValue, timestampHeaderValue);
        var result = Dt1520AuthenticatorCallbackValidationResult.FromSignatureResult(signatureResult, bodyBytes.Length);

        if (!result.IsValid && result.FailureKind is not null)
        {
            LogValidationFailure(result.FailureKind.Value);
        }

        return result;
    }

    private static async Task<byte[]> ReadRequestBodyAsync(
        HttpRequest request,
        long maxBodyBytes,
        CancellationToken cancellationToken)
    {
        ResetBodyPosition(request);

        await using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        if (memory.Length > maxBodyBytes)
        {
            throw new InvalidDataException("Callback request body exceeded the configured limit.");
        }

        return memory.ToArray();
    }

    private static void ResetBodyPosition(HttpRequest request)
    {
        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }
    }

    private void LogValidationFailure(Dt1520AuthenticatorCallbackValidationFailureKind failureKind)
    {
        _logger?.LogWarning(
            "DT-1520 callback validation failed with reason {FailureKind}.",
            failureKind);
    }
}
