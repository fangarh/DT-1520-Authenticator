using System.Security.Cryptography;
using System.Text;
using Dt1520.Authenticator.AspNetCore;
using Dt1520.Authenticator.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.AspNetCore.Tests;

public sealed class AspNetCoreCallbackValidatorTests
{
    private const string SigningSecret = "callback-secret";
    private const string PayloadJson = "{\"eventType\":\"challenge.approved\",\"challenge\":{\"status\":\"approved\"}}";

    [Fact]
    public async Task ValidateAsyncAcceptsSignedRawBodyAndResetsRequestBody()
    {
        var context = CreateContext(PayloadJson, CreateSignature(PayloadJson));
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(context.Request);

        Assert.True(result.IsValid);
        Assert.Equal(Encoding.UTF8.GetByteCount(PayloadJson), result.BodyLength);
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        Assert.Equal(PayloadJson, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ValidateAsyncRejectsTamperedBodyWithStableFailure()
    {
        var context = CreateContext(PayloadJson.Replace("approved", "denied", StringComparison.Ordinal), CreateSignature(PayloadJson));
        var logger = new ListLogger<Dt1520AuthenticatorCallbackValidator>();
        var validator = CreateValidator(logger);

        var result = await validator.ValidateAsync(context.Request);

        Assert.False(result.IsValid);
        Assert.Equal(Dt1520AuthenticatorCallbackValidationFailureKind.SignatureMismatch, result.FailureKind);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.RecommendedStatusCode);
        Assert.DoesNotContain(SigningSecret, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(PayloadJson, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SigningSecret, string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
        Assert.DoesNotContain(PayloadJson, string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsyncRejectsBodyAboveConfiguredLimitBeforeSignatureCheck()
    {
        var context = CreateContext(PayloadJson, CreateSignature(PayloadJson));
        context.Request.ContentLength = Encoding.UTF8.GetByteCount(PayloadJson) + 1;
        var validator = CreateValidator(maxBodyBytes: 8);

        var result = await validator.ValidateAsync(context.Request);

        Assert.False(result.IsValid);
        Assert.Equal(Dt1520AuthenticatorCallbackValidationFailureKind.BodyTooLarge, result.FailureKind);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.RecommendedStatusCode);
    }

    [Fact]
    public async Task FailureResultUsesSanitizedProblemResponse()
    {
        var validation = Dt1520AuthenticatorCallbackValidationResult.Failed(
            Dt1520AuthenticatorCallbackValidationFailureKind.SignatureMismatch);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await validation.ToFailureHttpResult().ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("DT-1520 callback validation failed", body, StringComparison.Ordinal);
        Assert.DoesNotContain(SigningSecret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(PayloadJson, body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, Dt1520AuthenticatorCallbackValidationFailureKind.MissingSignature)]
    [InlineData("sha1=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", Dt1520AuthenticatorCallbackValidationFailureKind.UnsupportedAlgorithm)]
    public async Task ValidateAsyncMapsFrameworkAgnosticSignatureFailures(
        string? signature,
        Dt1520AuthenticatorCallbackValidationFailureKind expectedFailureKind)
    {
        var context = CreateContext(PayloadJson, signature);
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(context.Request);

        Assert.False(result.IsValid);
        Assert.Equal(expectedFailureKind, result.FailureKind);
    }

    private static Dt1520AuthenticatorCallbackValidator CreateValidator(
        ILogger<Dt1520AuthenticatorCallbackValidator>? logger = null,
        long maxBodyBytes = 128 * 1024)
    {
        var options = new Dt1520AuthenticatorAspNetCoreOptions
        {
            BaseUrl = new Uri("https://auth.test/"),
            ClientId = "client-one",
            ClientSecret = "secret-one",
            CallbackSigningSecret = SigningSecret,
            MaxCallbackBodyBytes = maxBodyBytes,
        };

        return new Dt1520AuthenticatorCallbackValidator(new FixedOptionsMonitor(options), logger);
    }

    private static DefaultHttpContext CreateContext(string payloadJson, string? signature)
    {
        var context = new DefaultHttpContext();
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        context.Request.Body = new MemoryStream(payloadBytes);
        context.Request.ContentLength = payloadBytes.Length;
        context.Request.Headers.ContentType = "application/json";
        if (signature is not null)
        {
            context.Request.Headers[CallbackSignatureVerifier.SignatureHeaderName] = signature;
        }

        return context;
    }

    private static string CreateSignature(string payloadJson)
    {
        var signatureBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningSecret),
            Encoding.UTF8.GetBytes(payloadJson));

        return $"sha256={Convert.ToHexString(signatureBytes).ToLowerInvariant()}";
    }

    private sealed class FixedOptionsMonitor(Dt1520AuthenticatorAspNetCoreOptions value)
        : IOptionsMonitor<Dt1520AuthenticatorAspNetCoreOptions>
    {
        public Dt1520AuthenticatorAspNetCoreOptions CurrentValue => value;

        public Dt1520AuthenticatorAspNetCoreOptions Get(string? name)
        {
            return value;
        }

        public IDisposable? OnChange(Action<Dt1520AuthenticatorAspNetCoreOptions, string?> listener)
        {
            return null;
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
