# Dt1520.Authenticator.Client

Framework-agnostic .NET client package for DT-1520 Authenticator integration APIs.

Planned responsibilities:

- OAuth 2.0 `client_credentials` token acquisition.
- Token caching with an expiry skew and cancellation/timeout-aware HTTP calls.
- Typed challenge, device lookup and online TOTP verification calls.
- Stable SDK result/error mapping for backend `ProblemDetails`.
- Framework-agnostic callback signature validation over original payload bytes.

This package must not depend on ASP.NET Core and must not log or expose client secrets, bearer tokens, callback secrets, device tokens or raw callback payloads.

## Install

```powershell
dotnet add package Dt1520.Authenticator.Client --prerelease
```

Current prerelease version: `0.1.0-alpha.1`.

## Current prerelease surface

`Dt1520AuthenticatorClient` currently provides the core HTTP foundation and first typed integration calls:

- `Dt1520AuthenticatorClientOptions` validates the DT-1520 base URL, trusted backend credentials, optional scope, request timeout and token expiry skew.
- `AuthenticateAsync` requests `/oauth2/token` with `application/x-www-form-urlencoded` OAuth 2.0 `client_credentials` data and caches the returned bearer token until the configured expiry skew.
- Internal authorized JSON request plumbing attaches `Authorization: Bearer` only for request paths under the configured DT-1520 base URL.
- Expected DT-1520 `ProblemDetails` responses map to stable `Dt1520AuthenticatorErrorKind` values.
- `CreateChallengeAsync` maps to `POST /api/v1/challenges` and sends `Idempotency-Key` as an HTTP header when provided.
- `GetChallengeAsync` maps to `GET /api/v1/challenges/{challengeId}`.
- `VerifyTotpAsync` maps to `POST /api/v1/challenges/{challengeId}/verify-totp` and validates six-digit codes before sending.
- `ListDevicesForRoutingAsync` maps to `GET /api/v1/devices` and returns only integration-visible routing metadata.
- `SelectSinglePushDeviceAsync` returns a typed selected/no-device/ambiguous result for push routing.
- `PushChallengeOutcome` maps `pending`, `approved`, `denied`, `expired` and `failed` challenge states for workflow code.
- `CallbackSignatureVerifier` validates `X-OTPAuth-Signature` values in the current `sha256=<hex>` backend contract using HMAC-SHA256 over the original request body bytes.
- `CallbackSignatureVerificationResult` returns typed failure reasons for missing signatures, invalid format, unsupported algorithms, timestamp tolerance failures and signature mismatches.

`client_secret` belongs only in trusted backend configuration. Do not pass this package's credentials to desktop apps, browsers or logs. Do not log `VerifyTotpRequest.Code`; the SDK redacts it from `ToString`, but application logs must still avoid serializing request bodies.

Device routing models do not expose mobile `pushToken`, `publicKey`, `installationId`, device access tokens or refresh tokens. Selection helpers do not make local device trust decisions; they only help choose from backend-returned active push-capable candidates. When no device or multiple devices are available, integrators should ask the user/backend policy to disambiguate or fall back to online TOTP verification where policy allows it.

Callback verification must receive the raw HTTP request body bytes exactly as DT-1520 signed them. Do not validate a JSON object after parsing and serializing it again. Timestamp validation is applied only when a timestamp header value is supplied; the current DT-1520 callback/webhook contract signs the payload through `X-OTPAuth-Signature` and does not require a timestamp header.

## Basic backend usage

```csharp
using var authenticator = new Dt1520AuthenticatorClient(new Dt1520AuthenticatorClientOptions
{
    BaseUrl = new Uri("https://auth.example.test/"),
    Credentials = new Dt1520AuthenticatorClientCredentials(
        "integration-client-id",
        configuration["Dt1520:ClientSecret"]!),
});

var challenge = await authenticator.CreateChallengeAsync(new CreateChallengeRequest
{
    TenantId = tenantId,
    ApplicationClientId = applicationClientId,
    Subject = new ChallengeSubject { ExternalUserId = externalUserId },
    Operation = new ChallengeOperation
    {
        Type = ChallengeOperationType.StepUp,
        DisplayName = "Update VCS credentials",
    },
    PreferredFactors = [ChallengeFactorType.Push, ChallengeFactorType.Totp],
    Callback = new ChallengeCallbackRegistration
    {
        Url = new Uri("https://backend.example.test/dt1520/callbacks/challenges"),
    },
    CorrelationId = operationId,
    IdempotencyKey = operationId,
}, cancellationToken);

if (!challenge.IsSuccess)
{
    return challenge.Error!.Kind;
}
```

For online TOTP fallback, create or reuse an active challenge and call:

```csharp
var verified = await authenticator.VerifyTotpAsync(
    challengeId,
    new VerifyTotpRequest { Code = codeFromUser },
    cancellationToken);
```

Commit the protected business operation only after DT-1520 returns an approved challenge state. Treat denied, expired and failed outcomes as terminal non-approval states.

## Troubleshooting

- `Unauthorized` or `Forbidden`: check integration client status, scopes and the latest rotated secret from Admin UI.
- `ValidationFailed`: inspect SDK request validation before sending malformed tenant, application, callback or TOTP values.
- `RateLimited`: retry according to backend policy; do not spin on TOTP verification.
- Callback mismatch: verify the exact raw request body bytes and `X-OTPAuth-Signature` value, not a reserialized JSON payload.
