# DT-1520 Authenticator .NET SDK

This workspace contains the prerelease NuGet package scaffold for official DT-1520 Authenticator integrations.

## Packages

- `Dt1520.Authenticator.Client` - framework-agnostic client package for OAuth token acquisition, typed Authenticator API calls, safe `ProblemDetails` mapping and callback signature validation.
- `Dt1520.Authenticator.AspNetCore` - ASP.NET Core registration, options binding, `IHttpClientFactory` and callback endpoint helpers.
- `Dt1520.Authenticator.Desktop` - desktop approval session helpers that call an integrator backend and never store DT-1520 integration secrets.

## Security boundary

Default desktop topology is:

```text
Desktop App -> Integrator Backend -> DT-1520 Authenticator
```

Store `client_secret` only in the integrator backend or another server-side secret store. Do not put integration secrets, bearer tokens, device tokens, raw callback payloads or QR activation payloads in desktop configuration, package metadata, README examples or logs.

## Local commands

Run from this directory:

```powershell
dotnet restore .\Dt1520.Authenticator.slnx
dotnet build .\Dt1520.Authenticator.slnx
dotnet test .\Dt1520.Authenticator.slnx
dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release
```

Package projects target `net8.0`, enable nullable annotations, treat warnings as errors, build deterministically and include XML documentation plus package README files.

## Getting started for backend integrators

1. Create an integration client in Admin UI under `Integration clients`.
2. Copy the generated `clientSecret` once and store it only in backend secret storage.
3. Configure the backend with the DT-1520 base URL, `clientId`, `clientSecret` and callback signing secret.
4. Register `Dt1520.Authenticator.AspNetCore` from backend startup code.
5. Create a challenge before the protected business change is committed.
6. Validate DT-1520 callbacks over the original HTTP request body bytes.
7. Commit the protected business change only after the challenge is `approved`.
8. Handle `denied`, `expired`, `failed`, timeout and fallback-to-online-TOTP paths explicitly.

Minimal ASP.NET Core sample flow:

```csharp
builder.Services.AddDt1520Authenticator(
    builder.Configuration.GetSection("Dt1520Authenticator"));

app.MapPost("/vcs-credentials/approval/start", async (
    Dt1520AuthenticatorClient authenticator,
    ProtectedOperationDraft draft,
    CancellationToken cancellationToken) =>
{
    var challenge = await authenticator.CreateChallengeAsync(new CreateChallengeRequest
    {
        TenantId = draft.TenantId,
        ApplicationClientId = draft.ApplicationClientId,
        Subject = new ChallengeSubject { ExternalUserId = draft.ExternalUserId },
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
        CorrelationId = draft.OperationId,
        IdempotencyKey = draft.OperationId,
    }, cancellationToken);

    return challenge.IsSuccess
        ? Results.Accepted($"/vcs-credentials/approval/{challenge.Value!.Id}", challenge.Value)
        : Results.Problem(challenge.Error!.Title, statusCode: challenge.Error.StatusCode);
});

app.MapPost("/dt1520/callbacks/challenges", async (
    HttpRequest request,
    Dt1520AuthenticatorCallbackValidator callbackValidator,
    CancellationToken cancellationToken) =>
{
    var validation = await callbackValidator.ValidateAsync(request, cancellationToken);
    if (!validation.IsValid)
    {
        return validation.ToFailureHttpResult();
    }

    var callback = await request.ReadFromJsonAsync<ChallengeCallbackEnvelope>(
        cancellationToken: cancellationToken);

    if (callback?.Status == ChallengeStatus.Approved)
    {
        // Load the pending draft by callback.CorrelationId and commit it once.
        return Results.NoContent();
    }

    // Mark denied, expired or failed without applying the protected change.
    return Results.Accepted();
});
```

See `samples/aspnetcore-protected-operation/README.md` for the full documented flow shape, including status polling and online TOTP fallback.

See `PRERELEASE-HANDOFF.md` for the internal prerelease gate, package inspection checklist, security checklist and exact SDK APIs expected by the next `rdb_stand/` reference implementation.

## Client package status

`Dt1520.Authenticator.Client` now contains the core HTTP/token/problem foundation plus the first typed challenge APIs:

- validates base URL, backend-only integration credentials, optional scope, request timeout and token expiry skew;
- requests `/oauth2/token` with OAuth 2.0 `client_credentials` form data;
- caches bearer tokens until expiry minus skew;
- maps DT-1520 `ProblemDetails` into stable SDK error categories;
- distinguishes cancellation, timeout and network transport failures;
- prevents bearer auth attachment outside the configured DT-1520 base URL.
- creates challenges through `CreateChallengeAsync`;
- reads challenge state through `GetChallengeAsync`;
- verifies online TOTP codes through `VerifyTotpAsync`.
- lists integration-visible device routing candidates through `ListDevicesForRoutingAsync`;
- selects a single active push-capable target through `SelectSinglePushDeviceAsync`, returning `selected`, `no active push-capable device` or `ambiguous active push-capable devices`;
- maps push challenge states through `PushChallengeOutcome`.
- verifies callback and webhook signatures through `CallbackSignatureVerifier` over the original request body bytes and `X-OTPAuth-Signature` values such as `sha256=<hex>`.

Current public token values, TOTP request codes, callback signing secrets and device display labels are redacted from relevant `ToString` output; do not write `AccessToken`, `ClientSecret`, user verification codes, callback signing secrets, callback payloads, QR activation payloads or mobile device tokens to logs. Callback signature validation must use the raw request body bytes received from HTTP, not JSON parsed and serialized again by the integrator backend.

## ASP.NET Core package status

`Dt1520.Authenticator.AspNetCore` now contains backend-hosted integration helpers:

- `AddDt1520Authenticator(configuration)` and `AddDt1520Authenticator(options => ...)` register SDK services.
- `Dt1520AuthenticatorClient` is resolved through the named `IHttpClientFactory` client `Dt1520.Authenticator`; callers can customize the returned `IHttpClientBuilder`.
- `Dt1520AuthenticatorAspNetCoreOptions` validates the Authenticator base URL, integration client credentials, callback signing secret, timeouts and safe callback body size.
- `Dt1520AuthenticatorCallbackValidator` validates `X-OTPAuth-Signature` over the original ASP.NET Core request body bytes, uses request buffering and resets the body position for later model binding or JSON parsing.
- callback validation results and logs expose only stable failure kinds and sanitized status codes.

Keep `ClientSecret` and `CallbackSigningSecret` in server-side configuration. This package does not apply protected business changes for you; commit those changes only after your application has verified an approved DT-1520 result.

## Desktop package status

`Dt1520.Authenticator.Desktop` now contains desktop-safe approval session helpers:

- `DesktopApprovalSession` models waiting, approved, denied, expired, failed and cancelled states returned by an integrator backend.
- `DesktopApprovalPoller` polls the integrator backend through a caller-provided `HttpClient` and a relative polling path; it does not call DT-1520 directly.
- `DesktopApprovalOutcome` separates approved/denied/expired/failed/cancelled outcomes from local timeout.
- `DesktopApprovalPollingOptions` accepts only an integrator backend base URL, polling interval, timeout and safe response-size limit.

The desktop package has no dependency on `Dt1520.Authenticator.Client` and exposes no `client_secret`, bearer token or direct DT-1520 base URL configuration.

## Versioning and compatibility

- Current prerelease package version is `0.1.0-alpha.1`.
- Prerelease suffixes indicate unstable public SDK surface until the first stable package.
- Package versions follow SemVer: breaking public API changes require a major version bump after `1.0.0`.
- The first compatibility target is the DT-1520 Authenticator `v1` HTTP contract used by the current backend.
- `net8.0` is the first target framework; add legacy or extra targets only after a real consumer requirement.

## Troubleshooting

- `Unauthorized` or `Forbidden`: verify the Admin UI integration client status, scopes and latest rotated secret.
- Callback signature failures: validate the original request body bytes and the `X-OTPAuth-Signature` header before JSON parsing changes the payload.
- `Denied`, `Expired` or `Failed`: do not commit the protected operation; surface a retry or fallback flow according to backend policy.
- Desktop polling timeout: keep polling against the integrator backend only and show a local timeout state without assuming DT-1520 approval.
- Online TOTP fallback: use `VerifyTotpAsync` only for an active challenge and never log the submitted code.
