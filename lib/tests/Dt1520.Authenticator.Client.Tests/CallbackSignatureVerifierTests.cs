using System.Security.Cryptography;
using System.Text;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class CallbackSignatureVerifierTests
{
    private const string SigningSecret = "callback-secret";
    private const string PayloadJson = "{\"eventId\":\"evt_001\",\"eventType\":\"challenge.approved\",\"challenge\":{\"status\":\"approved\"}}";

    [Fact]
    public void VerifyAcceptsKnownGoodSignatureVector()
    {
        var payloadBytes = Encoding.UTF8.GetBytes(PayloadJson);
        var verifier = new CallbackSignatureVerifier(SigningSecret);

        var result = verifier.Verify(payloadBytes, CreateSignature(PayloadJson, SigningSecret));

        Assert.True(result.IsValid);
        Assert.Null(result.FailureKind);
    }

    [Fact]
    public void VerifyRejectsTamperedPayloadWithoutReserializingJson()
    {
        var signature = CreateSignature(PayloadJson, SigningSecret);
        var tamperedPayloadBytes = Encoding.UTF8.GetBytes(PayloadJson.Replace("approved", "denied", StringComparison.Ordinal));
        var verifier = new CallbackSignatureVerifier(SigningSecret);

        var result = verifier.Verify(tamperedPayloadBytes, signature);

        Assert.False(result.IsValid);
        Assert.Equal(CallbackSignatureVerificationFailureKind.SignatureMismatch, result.FailureKind);
    }

    [Theory]
    [InlineData(null, CallbackSignatureVerificationFailureKind.MissingSignature)]
    [InlineData("", CallbackSignatureVerificationFailureKind.MissingSignature)]
    [InlineData("sha256", CallbackSignatureVerificationFailureKind.InvalidFormat)]
    [InlineData("sha256=abc", CallbackSignatureVerificationFailureKind.InvalidFormat)]
    [InlineData("sha256=zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", CallbackSignatureVerificationFailureKind.InvalidFormat)]
    [InlineData("sha1=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", CallbackSignatureVerificationFailureKind.UnsupportedAlgorithm)]
    public void VerifyReturnsStableFailureReasonForInvalidSignatureHeader(
        string? signature,
        CallbackSignatureVerificationFailureKind expectedFailureKind)
    {
        var verifier = new CallbackSignatureVerifier(SigningSecret);

        var result = verifier.Verify(Encoding.UTF8.GetBytes(PayloadJson), signature);

        Assert.False(result.IsValid);
        Assert.Equal(expectedFailureKind, result.FailureKind);
    }

    [Fact]
    public void VerifyAcceptsUppercaseHexSignature()
    {
        var payloadBytes = Encoding.UTF8.GetBytes(PayloadJson);
        var verifier = new CallbackSignatureVerifier(SigningSecret);
        var signature = CreateSignature(PayloadJson, SigningSecret).ToUpperInvariant();

        var result = verifier.Verify(payloadBytes, signature);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyAppliesTimestampToleranceWhenTimestampIsProvided()
    {
        var payloadBytes = Encoding.UTF8.GetBytes(PayloadJson);
        var verifier = new CallbackSignatureVerifier(new CallbackSignatureVerifierOptions
        {
            SigningSecret = SigningSecret,
            TimestampTolerance = TimeSpan.FromMinutes(5),
        });

        var result = verifier.Verify(
            payloadBytes,
            CreateSignature(PayloadJson, SigningSecret),
            timestampHeaderValue: "1777284000",
            nowUtc: DateTimeOffset.FromUnixTimeSeconds(1777284210));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyRejectsTimestampOutsideTolerance()
    {
        var payloadBytes = Encoding.UTF8.GetBytes(PayloadJson);
        var verifier = new CallbackSignatureVerifier(new CallbackSignatureVerifierOptions
        {
            SigningSecret = SigningSecret,
            TimestampTolerance = TimeSpan.FromMinutes(5),
        });

        var result = verifier.Verify(
            payloadBytes,
            CreateSignature(PayloadJson, SigningSecret),
            timestampHeaderValue: "2026-04-27T10:00:00Z",
            nowUtc: new DateTimeOffset(2026, 4, 27, 10, 6, 0, TimeSpan.Zero));

        Assert.False(result.IsValid);
        Assert.Equal(CallbackSignatureVerificationFailureKind.TimestampOutsideTolerance, result.FailureKind);
    }

    [Fact]
    public void VerifyRejectsInvalidTimestampFormat()
    {
        var verifier = new CallbackSignatureVerifier(SigningSecret);

        var result = verifier.Verify(
            Encoding.UTF8.GetBytes(PayloadJson),
            CreateSignature(PayloadJson, SigningSecret),
            timestampHeaderValue: "not-a-timestamp");

        Assert.False(result.IsValid);
        Assert.Equal(CallbackSignatureVerificationFailureKind.InvalidFormat, result.FailureKind);
    }

    [Fact]
    public void SecretAndPayloadAreRedactedFromPublicToStringValues()
    {
        var options = new CallbackSignatureVerifierOptions
        {
            SigningSecret = SigningSecret,
            TimestampTolerance = TimeSpan.FromMinutes(5),
        };
        var verifier = new CallbackSignatureVerifier(options);
        var result = verifier.Verify(Encoding.UTF8.GetBytes(PayloadJson), CreateSignature(PayloadJson, SigningSecret));

        Assert.DoesNotContain(SigningSecret, options.ToString(), StringComparison.Ordinal);
        Assert.Contains("[redacted]", options.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SigningSecret, verifier.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(PayloadJson, result.ToString(), StringComparison.Ordinal);
    }

    private static string CreateSignature(string payloadJson, string signingSecret)
    {
        var signatureBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingSecret),
            Encoding.UTF8.GetBytes(payloadJson));

        return $"sha256={Convert.ToHexString(signatureBytes).ToLowerInvariant()}";
    }
}
