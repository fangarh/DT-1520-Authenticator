# Official Dotnet Integration SDK

## Status

Accepted productization track.

Iteration status:

- `Iteration 0` - completed as contract preflight on `2026-04-27`.
- `Iteration 1` - completed as repository and package scaffold under `lib/` on `2026-04-27`.
- `Iteration 2` - completed as core HTTP/token/problem foundation on `2026-04-27`.
- `Iteration 3` - completed as challenges and online `TOTP` typed client on `2026-04-27`.
- `Iteration 4` - completed as device lookup and push workflow helpers on `2026-04-27`.
- `Iteration 5` - completed as framework-agnostic callback signature validation on `2026-04-27`.
- `Iteration 6` - completed as `ASP.NET Core` DI/options/callback helper package on `2026-04-27`.
- `Iteration 7` - completed as desktop session and polling helper package without secret-bearing configuration on `2026-04-27`.
- `Iteration 8` - completed as package documentation, sample backend flow and docs app SDK section on `2026-04-27`.
- `Iteration 9` - completed as prerelease closure and handoff to [[Reference Desktop Backend Stand]] on `2026-04-27`.

## Goal

Создать официальный `.NET` SDK, который снижает стоимость интеграции с `DT-1520 Authenticator` для backend-сервисов и controlled desktop-сценариев, не размывая security boundary вокруг integration secrets.

Фактический repo root для будущих NuGet libraries: `lib/`.

## Required order

Этот track начинается только после:

1. [[Admin Client Management Follow-Up]]
2. [[QR Device Onboarding Follow-Up]]

Причина: SDK должен опираться на operator-ready client onboarding и воспроизводимую Android activation path, а не на ручные seed/debug workaround-и.

## Package layout

### `Dt1520.Authenticator.Client`

Базовая библиотека:

- `OAuth 2.0 client_credentials` token acquisition
- typed API client для challenges, devices lookup и `TOTP` verification
- `ProblemDetails` mapping в стабильные SDK errors/results
- timeout/retry/cancellation boundaries
- correlation/idempotency helpers where applicable
- no dependency on `ASP.NET Core`

### `Dt1520.Authenticator.AspNetCore`

Backend integration helpers:

- `IServiceCollection` extension methods
- `IHttpClientFactory` registration
- options/config binding
- callback signature validation
- endpoint/controller helper contracts
- structured logging hooks without secret leakage

### `Dt1520.Authenticator.Desktop`

Desktop-facing helper layer:

- approval session state model
- polling helper against the integrator backend
- typed UI outcomes: `approved`, `denied`, `expired`, `failed`
- cancellation/timeout helpers
- no default storage or embedding of `client_secret`

## Security model

Default desktop integration model:

`Desktop App -> Integrator Backend -> DT-1520 Authenticator`

The desktop app must not hold `integration client_secret` as a normal confidential secret. Direct desktop-to-Authenticator mode is out of default scope and requires a separate ADR.

SDK docs must clearly state:

- where `client_secret` may be stored
- which layer calls `DT-1520`
- how callback signatures are verified
- how `Denied/Expired/Failed` outcomes should be handled
- that business changes are committed only after `Approved`

## First implementation slice

Minimum useful prerelease:

- create challenge
- get challenge
- verify `TOTP` code
- validate callback signature
- map backend `ProblemDetails`
- sample ASP.NET Core backend flow
- unit tests for request mapping, auth header behavior, signature validation and error mapping

## 10-iteration execution plan

1. `Iteration 0` - contract preflight, public API outline and security boundaries.
2. `Iteration 1` - `lib/` solution, package projects, tests and packable metadata scaffold.
3. `Iteration 2` - core HTTP foundation: options, token acquisition, token cache, `ProblemDetails` mapping.
4. `Iteration 3` - challenge lifecycle and online `TOTP` verify typed client.
5. `Iteration 4` - device lookup and push workflow helpers.
6. `Iteration 5` - framework-agnostic callback signature validation.
7. `Iteration 6` - `ASP.NET Core` DI/options/callback helper package.
8. `Iteration 7` - desktop session and polling helper package without secret-bearing configuration.
9. `Iteration 8` - package documentation, sample backend flow and docs app SDK section.
10. `Iteration 9` - prerelease closure, security review and handoff to [[Reference Desktop Backend Stand]].

## Iteration 0 outcome

### Context7 documentation check

Current `.NET` and `NuGet` guidance was checked through `Context7` on `2026-04-27`:

- `/dotnet/docs` for library targeting, SemVer prerelease versions and nullable guidance.
- `/nuget/docs.microsoft.com-nuget` for SDK-style package metadata, `PackageReadmeFile`, repository metadata, license expression and package icon/readme packaging.

Decisions derived from that check:

- prerelease packages use SemVer suffixes such as `0.1.0-alpha.1` or `0.1.0-preview.1`;
- SDK-style projects should carry package metadata in project files or shared MSBuild props;
- package README files must be explicitly included through `PackageReadmeFile`;
- package metadata should include `PackageId`, `Version`, `Authors`, `Description`, `PackageTags`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl` and `RepositoryType`;
- package icons are optional for the first internal prerelease, but if added they must be packaged intentionally and must not block Iteration 1;
- XML documentation is required for public SDK APIs;
- `netstandard2.0` is not the default target unless a real consumer needs .NET Framework or broad legacy reach.

### Accepted package identities

- `Dt1520.Authenticator.Client`
- `Dt1520.Authenticator.AspNetCore`
- `Dt1520.Authenticator.Desktop`

Namespace policy:

- root namespace matches package id;
- public API uses `Dt1520.Authenticator.*`;
- no generic `Common`, `Shared`, `Utils` or catch-all namespaces for public surface.

### Accepted target framework policy

Prerelease baseline:

- target `net8.0` for all three packages in Iteration 1;
- do not add `netstandard2.0` by default;
- do not add `net10.0` in the first scaffold unless repo/runtime baseline work creates a concrete need;
- keep multi-targeting as a later explicit decision if an integrator requires .NET Framework, `netstandard2.0`, or current-runtime-specific APIs.

Rationale:

- `net8.0` is a conservative LTS-friendly baseline for backend integrators;
- SDK code benefits from modern BCL APIs, nullable annotations and current packaging behavior;
- adding legacy targets early increases API constraints and test matrix before a real consumer need exists.

### Accepted public API style

General style:

- async-first APIs with `CancellationToken` on all I/O methods;
- immutable request/response records for transport-facing contracts;
- explicit options objects with validation before first network call;
- public XML docs for all public types and members;
- nullable reference types enabled;
- warnings treated as errors;
- no public APIs that require consumers to catch raw `HttpRequestException` for expected `DT-1520` `ProblemDetails` responses.

Result and error semantics:

- expected remote outcomes return typed SDK results/errors;
- transport failures, cancellation and timeout are distinguishable from backend `ProblemDetails`;
- backend `ProblemDetails` maps to stable SDK categories:
  - `Unauthorized`
  - `Forbidden`
  - `ValidationFailed`
  - `Conflict`
  - `NotFound`
  - `RateLimited`
  - `ServerFailure`
  - `TransportFailure`
- raw status code, trace/correlation ids and safe problem metadata may be exposed through sanitized diagnostic fields;
- secrets, bearer tokens, callback secrets, raw callback payloads and full request/response bodies are never included in `ToString`, exception messages or default logs.

### Accepted package boundaries

`Dt1520.Authenticator.Client`:

- owns base URL handling, OAuth `client_credentials`, token cache, typed HTTP API, `ProblemDetails` mapping and framework-agnostic callback signature validation;
- has no dependency on `ASP.NET Core`;
- may use BCL and carefully selected `Microsoft.Extensions.*` abstractions only when they do not force ASP.NET Core hosting.

`Dt1520.Authenticator.AspNetCore`:

- owns DI registration, options binding/validation, `IHttpClientFactory` integration, callback endpoint helpers and logging hooks;
- may depend on `Microsoft.Extensions.*` and ASP.NET Core abstractions where justified;
- must not hide the business rule that protected operations are committed only after `approved`.

`Dt1520.Authenticator.Desktop`:

- owns desktop-facing approval session state and polling helpers against the integrator backend;
- has no direct `DT-1520` client and no integration `client_secret` options;
- does not choose a concrete UI framework in the first prerelease.

### Accepted SDK surface outline

Initial `Client` package public concepts:

- `Dt1520AuthenticatorClientOptions`
- `Dt1520AuthenticatorClient`
- `Dt1520AuthenticatorClientCredentials`
- `Dt1520AuthenticatorResult<T>`
- `Dt1520AuthenticatorError`
- `Dt1520AuthenticatorErrorKind`
- `CreateChallengeRequest`
- `ChallengeResponse`
- `ChallengeStatus`
- `VerifyTotpRequest`
- `VerifyTotpResult`
- `CallbackSignatureVerifier`
- `CallbackSignatureVerificationResult`
- `CallbackSignatureVerificationFailureKind`

Initial `AspNetCore` package public concepts:

- `Dt1520AuthenticatorAspNetCoreOptions`
- `AddDt1520Authenticator(...)`
- callback validation helpers that operate on raw request body bytes without JSON reserialization;
- safe logging extension points with redaction.

Initial `Desktop` package public concepts:

- `DesktopApprovalSession`
- `DesktopApprovalSessionStatus`
- `DesktopApprovalPollingOptions`
- `DesktopApprovalPoller`
- `DesktopApprovalOutcome`

Names may still change during Iteration 1 only if scaffold work reveals a simpler, more idiomatic public shape. Any such change must be recorded in this note before code expands.

### Security non-goals and hard boundaries

The SDK must not introduce:

- direct browser-to-Authenticator integration;
- default direct desktop-to-Authenticator integration;
- storage of `integration client_secret` in desktop app configuration;
- business operation commit helpers that can apply sensitive changes before `approved`;
- callback validation that reserializes JSON instead of validating the original payload bytes;
- logging of raw callback payloads, bearer tokens, client secrets, QR activation payloads or mobile device tokens;
- local device trust decisions in SDK code;
- backend-local `TOTP` verification cache that bypasses centralized replay defense, audit and rate limiting.

The desktop-safe default remains:

`Desktop App -> Integrator Backend -> DT-1520 Authenticator`

### Iteration 1 scaffold decisions

Iteration 1 should create:

- `lib/Dt1520.Authenticator.slnx` or `lib/Dt1520.Authenticator.sln` depending on current repo convention/tooling support at implementation time;
- `lib/src/Dt1520.Authenticator.Client`;
- `lib/src/Dt1520.Authenticator.AspNetCore`;
- `lib/src/Dt1520.Authenticator.Desktop`;
- `lib/tests/Dt1520.Authenticator.Client.Tests`;
- `lib/tests/Dt1520.Authenticator.AspNetCore.Tests`;
- `lib/tests/Dt1520.Authenticator.Desktop.Tests`;
- shared build/package props if they reduce duplication without hiding package-specific metadata;
- `lib/README.md`;
- package README placeholders included through `PackageReadmeFile`.

Initial project settings:

- `TargetFramework` = `net8.0`;
- `Nullable` = `enable`;
- `TreatWarningsAsErrors` = `true`;
- deterministic build enabled;
- XML documentation generation enabled for package projects;
- package metadata placeholders without private URLs, secrets or internal hostnames.

## Iteration 1 outcome

`lib/` now contains a reproducible SDK workspace:

- `lib/Dt1520.Authenticator.slnx`
- `lib/Directory.Build.props`
- `lib/Directory.Build.targets`
- `lib/src/Dt1520.Authenticator.Client`
- `lib/src/Dt1520.Authenticator.AspNetCore`
- `lib/src/Dt1520.Authenticator.Desktop`
- `lib/tests/Dt1520.Authenticator.Client.Tests`
- `lib/tests/Dt1520.Authenticator.AspNetCore.Tests`
- `lib/tests/Dt1520.Authenticator.Desktop.Tests`
- `lib/README.md`

Scaffold settings:

- all package projects target `net8.0`;
- nullable annotations, implicit usings, warnings-as-errors, deterministic build and CI build metadata are enabled through shared MSBuild props;
- package projects generate XML docs, include symbols, use `0.1.0-alpha.1`, and include package README files through `PackageReadmeFile`;
- package metadata uses public placeholder repository URLs and contains no private hostnames, credentials, secrets, bearer tokens, callback payloads or mobile device tokens;
- desktop package README explicitly keeps the default topology as `Desktop App -> Integrator Backend -> DT-1520 Authenticator`.

Scaffold tests:

- package-specific xUnit tests verify project metadata, shared build/package settings and README secret-boundary wording.

### Iteration 1 verification

Context/documentation check:

- current `.NET` SDK-style package guidance was checked through Context7 `/dotnet/docs`;
- current NuGet metadata and `PackageReadmeFile` guidance was checked through Context7 `/nuget/docs.microsoft.com-nuget`.

Local verification:

- `dotnet restore .\Dt1520.Authenticator.slnx` - passed outside sandbox after sandbox write denial;
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`6/6`);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed and produced `.nupkg` + `.snupkg` for all three packages;
- package inspection confirmed each `.nupkg` contains package-level `README.md` and XML documentation under `lib/net8.0`.

Environment note:

- initial sandbox `dotnet new` auto-restore and `dotnet build` attempts hit `Access denied` under `lib/artifacts/obj`;
- stale `dotnet`/`VBCSCompiler` processes from the failed run were stopped, only generated `lib/artifacts` was removed, and the same verification was rerun sequentially outside sandbox.

Security review:

- no runtime SDK HTTP/auth code exists yet;
- no real secrets, tokens, signing material, callback payloads, private credential URLs, QR activation payloads or mobile device tokens were added;
- package READMEs preserve the backend-only `client_secret` boundary;
- `Dt1520.Authenticator.Desktop` scaffold has no direct Authenticator client or secret-bearing configuration.

## Iteration 2 outcome

`Dt1520.Authenticator.Client` now contains the first runtime foundation:

- `Dt1520AuthenticatorClientOptions` validates absolute `http/https` base URL, backend-only client credentials, optional scope, request timeout and token expiry skew;
- `Dt1520AuthenticatorClientCredentials` redacts `client_secret` from `ToString`;
- `Dt1520AuthenticatorClient.AuthenticateAsync` requests `/oauth2/token` with `application/x-www-form-urlencoded` OAuth 2.0 `client_credentials` data;
- access tokens are cached until `expires_in` minus configured skew, with a clock abstraction for deterministic tests;
- internal authorized JSON plumbing attaches bearer auth only to request paths under the configured DT-1520 base URL;
- `ProblemDetails` responses map into stable `Dt1520AuthenticatorErrorKind` values: `ValidationFailed`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `RateLimited`, `ServerFailure`;
- timeout, caller cancellation and network transport failures are separated as `Timeout`, `Canceled` and `TransportFailure`.

Context7 documentation check:

- current `.NET` HTTP client guidance was checked through Context7 `/dotnet/docs` on `2026-04-27`;
- implementation follows the current preference for caller-provided `HttpClient` support and uses an internally owned `HttpClient` with `SocketsHttpHandler.PooledConnectionLifetime` for the direct constructor path;
- JSON handling uses `System.Text.Json` web defaults.

Tests:

- `lib/tests/Dt1520.Authenticator.Client.Tests/ClientHttpFoundationTests.cs` covers token request mapping, token cache reuse/refresh and authorized request base URL scoping.
- `lib/tests/Dt1520.Authenticator.Client.Tests/ClientErrorMappingTests.cs` covers `ProblemDetails` mapping plus cancellation, timeout and transport failure distinction.
- `lib/tests/Dt1520.Authenticator.Client.Tests/ClientSecretRedactionTests.cs` covers credentials/token redaction and validation exception safety.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`24/24`, including `20/20` client tests);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`24/24`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed, Release `.nupkg/.snupkg` artifacts are present for all three packages.

Security review:

- `client_secret` is accepted only through trusted backend options and is not included in `ToString` or validation exception details;
- bearer token values are redacted from `Dt1520AuthenticatorAccessToken.ToString`;
- expected `ProblemDetails` mapping does not retain raw response bodies;
- bearer `Authorization` is not attached to absolute URLs outside configured DT-1520 base URL;
- no `ASP.NET Core` dependency, desktop secret-bearing configuration, browser direct path, raw callback payload handling or mobile device token handling was introduced.

## Iteration 3 outcome

`Dt1520.Authenticator.Client` now exposes the minimum useful challenge/TOTP integration surface:

- `CreateChallengeAsync(CreateChallengeRequest, CancellationToken)` maps to `POST /api/v1/challenges`;
- `GetChallengeAsync(Guid, CancellationToken)` maps to `GET /api/v1/challenges/{challengeId}`;
- `VerifyTotpAsync(Guid, VerifyTotpRequest, CancellationToken)` maps to `POST /api/v1/challenges/{challengeId}/verify-totp`;
- public models include `CreateChallengeRequest`, `ChallengeSubject`, `ChallengeOperation`, `ChallengeCallbackRegistration`, `ChallengeResponse`, `VerifyTotpRequest`, `ChallengeFactorType`, `ChallengeOperationType` and `ChallengeStatus`;
- enum JSON mapping follows backend/OpenAPI snake_case values such as `step_up`, `backup_code`, `pending` and `approved`;
- `CreateChallengeRequest.IdempotencyKey` is sent as the `Idempotency-Key` HTTP header and is not serialized into the JSON body;
- SDK validation rejects empty tenant/application/challenge IDs, missing subject/operation, non-HTTP callback URLs, empty preferred factor lists, invalid idempotency header characters and non-six-digit TOTP codes before network calls;
- expected `410 Gone` and `422 Unprocessable Entity` challenge failures now map to stable SDK result errors instead of transport failures.

Tests:

- `ClientChallengeApiTests` covers create/get/verify request mapping, response mapping, auth header reuse, idempotency header behavior, validation-before-network and expected `ProblemDetails` mapping.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`34/34`, including `30/30` client tests);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`34/34`, including `30/30` client tests);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed.

Security review:

- bearer auth remains constrained to configured DT-1520 base URL by the existing HTTP pipeline;
- `VerifyTotpRequest.ToString()` redacts the user code;
- `CreateChallengeRequest.ToString()` avoids callback URL and user subject echoing;
- no `client_secret`, bearer token, callback payload, QR activation payload, mobile device token or desktop secret-bearing configuration was introduced;
- SDK validation reduces accidental malformed calls but does not bypass backend policy, replay defense, audit or rate limiting.

## Iteration 4 outcome

`Dt1520.Authenticator.Client` now exposes typed helpers for integration-visible device routing and push workflow state:

- `ListDevicesForRoutingAsync(externalUserId, pushCapableOnly)` maps to `GET /api/v1/devices?externalUserId=...&pushCapableOnly=...` and returns sanitized `DeviceRoutingCandidate` metadata.
- `SelectSinglePushDeviceAsync(externalUserId)` calls the push-capable lookup path and returns `PushDeviceSelectionResult`.
- `PushDeviceSelectionResult` distinguishes `Selected`, `NoActivePushCapableDevice` and `AmbiguousActivePushCapableDevices` without making local trust decisions.
- Public device models include only routing-safe metadata: device id, platform, lifecycle status, attestation status, optional display label, push-capable flag and timestamps.
- `PushChallengeOutcome.FromChallenge` maps `pending/approved/denied/expired/failed` challenge statuses into simple workflow outcomes for integrator code.
- No push token, public key, installation id, device access/refresh token, QR activation payload or desktop secret-bearing configuration was introduced.

Tests:

- `ClientDeviceRoutingTests` covers device lookup request mapping, query escaping, response enum/timestamp mapping, blank-input validation, expected `ProblemDetails` mapping, single/no-device/ambiguous selection outcomes, push outcome mapping and no-secret public model checks.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`47/47`, including `43/43` client tests);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`47/47`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed.

Security review:

- bearer auth remains scoped to the configured DT-1520 base URL through the existing HTTP pipeline;
- SDK device lookup uses the backend integration-visible contract and does not expose mobile push tokens, public keys, installation ids or device tokens;
- selection helpers only choose among backend-returned active push-capable candidates and do not infer device trust locally;
- no callback signature, raw callback payload, client secret, bearer token or QR activation payload handling was added in this iteration.

## Iteration 5 outcome

`Dt1520.Authenticator.Client` now exposes framework-agnostic callback and webhook signature verification:

- `CallbackSignatureVerifier` validates `X-OTPAuth-Signature` values in the current backend contract format `sha256=<hex>`.
- HMAC is computed with `HMACSHA256` over the original request body bytes supplied by the integrator; JSON parsing/reserialization is intentionally outside the verifier.
- Signature comparison uses `CryptographicOperations.FixedTimeEquals`.
- Public callback types include `CallbackSignatureVerifierOptions`, `CallbackSignatureVerificationResult` and `CallbackSignatureVerificationFailureKind`.
- Stable failure kinds cover missing signature, invalid format, unsupported algorithm, timestamp outside tolerance and signature mismatch.
- Optional timestamp tolerance is supported when an integrator supplies a timestamp header value, but the current backend/OpenAPI callback contract does not require a timestamp header.
- Signing secret and raw payload are not included in public `ToString()` output.

Context7 documentation check:

- current `.NET` cryptography guidance was checked through Context7 `/dotnet/docs` on `2026-04-27`;
- implementation uses the BCL cryptography APIs available to the package's `net8.0` baseline.

Tests:

- `CallbackSignatureVerifierTests` covers known-good signature vectors, tampered payload rejection, missing/malformed signature headers, unsupported algorithms, optional timestamp tolerance and secret/payload redaction.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`60/60`, including `56/56` client tests);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`60/60`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed.

Security review:

- verification accepts raw bytes and does not parse or reserialize callback JSON;
- callback signing secret is stored only in verifier options/runtime memory and is redacted from public string output;
- callback payload is never stored in result objects or public string output;
- unsupported algorithms fail closed;
- signature comparison is timing-safe for equal-length decoded signatures;
- no ASP.NET Core dependency, desktop secret-bearing configuration, browser direct path, client secret, bearer token, QR activation payload or mobile device token handling was introduced.

## Iteration 6 outcome

`Dt1520.Authenticator.AspNetCore` now exposes backend integration helpers around the existing `Client` package:

- `AddDt1520Authenticator(...)` registers SDK services from `IConfiguration` or code-based options.
- `Dt1520AuthenticatorAspNetCoreOptions` binds backend-hosted `BaseUrl`, integration `ClientId/ClientSecret`, callback signing secret, timeout/skew and safe callback body limits.
- options validation fail-closes missing URL, client credentials, callback signing secret, invalid timeouts and malformed product tokens without echoing secret values.
- `Dt1520AuthenticatorClient` is resolved through a named `IHttpClientFactory` registration `Dt1520.Authenticator`, and the returned `IHttpClientBuilder` can be customized by integrator code.
- `Dt1520AuthenticatorCallbackValidator` reads the original ASP.NET Core `HttpRequest.Body` bytes with buffering enabled, resets body position after validation and delegates HMAC validation to `CallbackSignatureVerifier`.
- callback validation results expose only stable failure kinds, body length and safe status codes; raw callback payload, callback signing secret, client secret and bearer token values are not retained in result objects or logs.
- `ToFailureHttpResult()` returns a sanitized problem response for minimal APIs/controllers and does not depend on business operation commit logic.

Context7 documentation check:

- current ASP.NET Core docs were checked through Context7 `/dotnet/aspnetcore.docs` on `2026-04-27`;
- implementation follows documented patterns for `AddOptions().Bind(...).Validate...`, typed/named client registration through `AddHttpClient`, and raw request body re-read via `EnableBuffering()` before reading the body.

Tests:

- `AspNetCoreRegistrationTests` covers configuration binding, DI registration, named `IHttpClientFactory` customization and secret-safe options validation.
- `AspNetCoreCallbackValidatorTests` covers signed raw-body validation, request body rewind, tampered body rejection, body-size fail-closed behavior, signature failure mapping and sanitized failure responses/logging.

Local verification:

- `dotnet restore .\Dt1520.Authenticator.slnx` - passed after project reference/framework reference changes;
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`70/70`, including `12/12` ASP.NET Core tests);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`70/70`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed; Release `.nupkg/.snupkg` artifacts are present for all three packages.
- docs verification passed after SDK handoff updates: `npm test`, `npm run build`, `npm run test:e2e`.

Security review:

- integration `client_secret` and callback signing secret remain server-side options and are redacted from `ToString`/validation paths;
- callback validation uses original request body bytes and does not parse or reserialize JSON;
- request body buffering is size-limited by `MaxCallbackBodyBytes` and resets the stream for downstream model binding/handlers;
- validation logs only stable failure kind values and never logs raw payload, signing secret, client secret or bearer token;
- no business commit helper was added, so integrators still must apply protected operations only after an approved DT-1520 result;
- no desktop direct-to-Authenticator secret-bearing configuration, browser direct path, QR activation payload handling or mobile device token handling was introduced.

## Iteration 7 outcome

`Dt1520.Authenticator.Desktop` now exposes desktop-safe approval session helpers:

- `DesktopApprovalSession` models waiting, approved, denied, expired, failed and cancelled states from an integrator backend.
- `DesktopApprovalPoller` polls only an integrator backend through a caller-provided `HttpClient`, configured backend base URL and relative polling path.
- `DesktopApprovalPollingOptions` contains backend base URL, polling interval, timeout and response-size limit only; it has no `client_secret`, bearer token, callback signing secret or direct DT-1520 base URL options.
- `DesktopApprovalOutcome` gives typed UI/workflow outcomes for approved, denied, expired, failed, cancelled and local timeout.
- Polling response parsing is size-limited, rejects absolute polling paths and does not expose raw backend failure bodies in outcome messages.
- The desktop package does not reference `Dt1520.Authenticator.Client`; DT-1520 calls remain backend-only.

Context7 documentation check:

- current `.NET` async polling/cancellation guidance was checked through Context7 `/dotnet/docs` on `2026-04-27`;
- implementation follows documented async polling with `Task.Delay(..., CancellationToken)`, passes cancellation through `HttpClient.SendAsync`, and returns typed cancellation/timeout outcomes instead of forcing UI code to inspect transport exceptions.

Tests:

- `DesktopApprovalPollerTests` covers terminal state short-circuiting, backend polling to approved, denied/expired/failed/cancelled status mapping, caller cancellation, local timeout, raw body non-disclosure, absolute polling path rejection and secret-boundary reflection checks.
- `DesktopPackageScaffoldTests` still covers package metadata and README security-boundary wording.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`82/82`, including `14/14` desktop tests);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`82/82`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed and produced the Desktop package artifacts;
- docs verification passed after SDK handoff updates: `npm test`, `npm run build`, `npm run test:e2e`.

Security review:

- desktop code has no integration `client_secret`, bearer token, callback signing secret, direct DT-1520 base URL or dependency on `Dt1520.Authenticator.Client`;
- polling paths must be relative and remain under the configured integrator backend origin;
- raw backend failure bodies are not included in `DesktopApprovalOutcome`;
- `ToString()` methods omit session id, polling path and backend URL to avoid accidental logging of opaque desktop session identifiers;
- no default storage, browser direct path, QR activation payload handling, mobile device token handling or business commit helper was introduced.

## Iteration 8 outcome

SDK documentation is now usable without reading backend source:

- `lib/README.md` documents the package map, backend getting started flow, minimal ASP.NET Core challenge/callback sample, SemVer/prerelease compatibility policy and troubleshooting.
- Package READMEs under `lib/src/Dt1520.Authenticator.Client|AspNetCore|Desktop/README.md` now include install commands, package-specific getting started guidance, callback/raw-body requirements, online `TOTP` fallback and desktop polling boundaries.
- Added `lib/samples/aspnetcore-protected-operation/README.md` as the documented backend flow for `start challenge -> validate callback -> status polling -> online TOTP fallback`.
- `docs/` now has a dedicated `.NET SDK` section that points to package READMEs, the sample backend flow and the desktop/backend secret boundary.
- SDK scaffold tests now assert documentation coverage for getting started, callback validation, approval-before-commit, sample backend flow and secret-safe examples.

Context7 documentation check:

- current `.NET` library documentation/package guidance was checked through Context7 `/dotnet/docs` on `2026-04-27`;
- current NuGet `PackageReadmeFile`, package metadata and prerelease package guidance was checked through Context7 `/nuget/docs.microsoft.com-nuget`.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`84/84`);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`84/84`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed;
- package inspection confirmed all three Release `.nupkg` files contain root `README.md` and `lib/net8.0/*.xml`;
- docs verification passed: `npm test` (`3/3`), `npm run build`, `npm run test:e2e` (`2/2`).

Security review:

- no runtime SDK behavior changed;
- documentation keeps `clientSecret` and `callbackSigningSecret` server-side and does not introduce browser/desktop direct-to-DT-1520 paths;
- sample flow commits protected business changes only after `ChallengeStatus.Approved` and treats denied/expired/failed as non-approval;
- callback validation examples use original request body bytes before JSON parsing changes the payload;
- README/docs/sample content does not add real secrets, bearer tokens, callback payloads, QR activation payloads, mobile device tokens, private URLs with credentials or raw backend error bodies.

## Iteration 9 outcome

SDK prerelease closure is complete:

- added `lib/PRERELEASE-HANDOFF.md` with package verification gate, package inspection checklist, security checklist and exact SDK APIs expected by `rdb_stand/`;
- added `rdb_stand/README.md` as the next-track entry point for `Desktop App -> Reference Backend -> DT-1520 Authenticator -> Android App`;
- added `PrereleaseHandoffTests` to keep the handoff/security boundary covered by automated tests;
- updated docs app `.NET SDK` content to point to the prerelease handoff.

Context7 documentation check:

- current `.NET` package/versioning guidance was checked through Context7 `/dotnet/docs` on `2026-04-27`;
- current NuGet metadata, readme and symbols guidance was checked through Context7 `/nuget/docs.microsoft.com-nuget`.

Local verification:

- `dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1` - passed (`86/86`);
- `dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1` - passed;
- `dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed (`86/86`);
- `dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1` - passed;
- package projects were also packed directly with `dotnet pack .\src\Dt1520.Authenticator.Client|AspNetCore|Desktop\*.csproj --no-build --configuration Release`;
- Release package inspection confirmed all three `.nupkg` files contain root `README.md`, `lib/net8.0/*.xml`, `0.1.0-alpha.1`, `MIT` license metadata and no checked secret markers in README/XML/nuspec content;
- docs verification passed: `npm test` (`3/3`), `npm run build`, `npm run test:e2e` (`2/2`).

Security review:

- no direct desktop-to-Authenticator secret-bearing path exists;
- `client_secret`, callback signing secret and DT-1520 bearer tokens remain backend-only;
- callback validation remains bound to original raw request body bytes;
- protected business state is still committed only after approved challenge status;
- `denied`, `expired`, `failed`, timeout and cancellation are documented as non-approval outcomes;
- package docs/handoff/readme content does not introduce raw callback payloads, TOTP codes, QR activation payloads, mobile device tokens, push tokens, private credential URLs or raw backend error bodies;
- the only code change was public XML-doc wording from `Bearer token value` to `Access token value` to avoid false-positive package marker scans.

Next continuation point:

- [[Reference Desktop Backend Stand]].

### Iteration 0 verification

Documentation review:

- 10-iteration plan exists in this note;
- package identities, target frameworks, public API style and package boundaries are explicitly accepted;
- Context7 `.NET/NuGet` guidance was checked before finalizing scaffold decisions.

Security review:

- no code was added;
- no secrets, tokens, signing material, private credential URLs, raw callback payloads or mobile push tokens were introduced into documentation;
- desktop direct secret-bearing path remains out of default scope;
- callback validation is required to use original payload bytes;
- `client_secret` is restricted to backend/integrator server configuration.

## Iteration Plan

### Iteration 0. SDK contract preflight

Goal:

- превратить этот productization track в implementation-ready contract до создания public API
- подтвердить target frameworks, package boundaries, naming, error/result semantics и security non-goals

Scope:

- зафиксировать package identities:
  - `Dt1520.Authenticator.Client`
  - `Dt1520.Authenticator.AspNetCore`
  - `Dt1520.Authenticator.Desktop`
- выбрать target frameworks для prerelease:
  - preferred baseline: `net8.0` для интеграторов на LTS/runtime-friendly backend stacks
  - optional current target: `net10.0`, если нужно использовать уже принятый repo/runtime baseline
  - `netstandard2.0` не включать автоматически без явного consumer need, чтобы не тащить устаревшие API constraints
- определить public API style:
  - async-first methods with `CancellationToken`
  - immutable request/response records
  - stable SDK result/errors вместо проброса raw `HttpRequestException` для expected backend `ProblemDetails`
  - raw HTTP details доступны только sanitized/debug-safe образом
- определить package dependency policy:
  - `Client` не зависит от `ASP.NET Core`
  - `AspNetCore` может зависеть от `Microsoft.Extensions.*`
  - `Desktop` не зависит от concrete UI framework в первом prerelease
- сверить актуальные .NET/NuGet guidance через `Context7` перед финализацией scaffold-а

Exit criteria:

- accepted API outline в этой заметке
- первый context reset prompt для SDK track: [[Official Dotnet Integration SDK Context Reset Prompt]]
- no code yet, кроме возможных placeholder README/solution notes

Verification:

- documentation review: completed in `Iteration 0 outcome`
- security review по boundaries `client_secret`, callback verification и desktop non-goals: completed in `Iteration 0 outcome`

### Iteration 1. Repository and package scaffold

Goal:

- создать воспроизводимый `lib/` workspace для будущих NuGet packages и тестов

Scope:

- добавить solution under `lib/`
- добавить projects:
  - `lib/src/Dt1520.Authenticator.Client`
  - `lib/src/Dt1520.Authenticator.AspNetCore`
  - `lib/src/Dt1520.Authenticator.Desktop`
  - `lib/tests/Dt1520.Authenticator.Client.Tests`
  - `lib/tests/Dt1520.Authenticator.AspNetCore.Tests`
  - `lib/tests/Dt1520.Authenticator.Desktop.Tests`
- включить `Nullable`, `TreatWarningsAsErrors`, deterministic build, XML docs для public API
- добавить package metadata placeholders: id, description, authors, repository URL, license/readme placeholders
- добавить `lib/README.md` с package map и local commands
- добавить verification script или documented command для `dotnet build/test/pack`

Exit criteria:

- пустой scaffold builds/tests/packs locally
- package metadata не содержит private URLs/secrets
- `Implementation Map` знает entry points для SDK workspace

Verification:

- `dotnet build lib/...`
- `dotnet test lib/...`
- `dotnet pack lib/... --no-build` или equivalent после build

### Iteration 2. `Dt1520.Authenticator.Client` core HTTP foundation

Goal:

- заложить безопасный HTTP/runtime фундамент без привязки к конкретным API endpoints

Scope:

- `Dt1520AuthenticatorClientOptions`:
  - base URL
  - client id/secret
  - timeout defaults
  - optional user agent/product info
- token acquisition через `OAuth 2.0 client_credentials`
- token cache with expiry skew, cancellation and clock abstraction
- auth header attachment только к configured base URL
- JSON serializer options compatible with backend contract
- `ProblemDetails` parser and SDK error model:
  - `Unauthorized`
  - `Forbidden`
  - `ValidationFailed`
  - `Conflict`
  - `NotFound`
  - `RateLimited`
  - `TransportFailure`
- no raw secret logging and no secret values in `ToString`

Exit criteria:

- token request/response path covered
- expired token refresh path covered
- expected backend errors return stable SDK result/errors

Verification:

- unit tests with fake `HttpMessageHandler`
- security tests for auth header behavior and secret redaction

### Iteration 3. Challenges and TOTP typed client

Goal:

- закрыть minimum useful prerelease для backend integrator-а

Scope:

- typed APIs:
  - create challenge
  - get challenge
  - verify TOTP code
- request models для `tenantId`, `applicationClientId`, `externalUserId`, factor preference, callback URL/correlation where supported
- response models для challenge status:
  - `pending`
  - `approved`
  - `denied`
  - `failed`
  - `expired`
- idempotency/correlation helpers where backend contract supports them
- strong validation before sending invalid SDK requests
- tests that request JSON matches OpenAPI/backend contract

Exit criteria:

- интегратор может создать challenge, прочитать статус и выполнить online TOTP verify через SDK
- expected `ProblemDetails` mapped to typed SDK outcomes

Verification:

- request mapping tests
- response mapping tests
- auth header tests
- validation tests

### Iteration 4. Device lookup and push workflow helpers

Goal:

- дать backend-интегратору typed support для device-bound push routing без раскрытия mobile internals

Scope:

- typed API для device lookup/routing candidates if exposed by integration contract
- helper для выбора push-capable active device only when backend contract allows it
- status/result helpers для `pending -> approved/denied/expired`
- no direct mobile token/public key exposure
- preserve backend authority: SDK не должен решать device trust locally

Exit criteria:

- SDK покрывает push challenge happy path and common no-device/ambiguous-device outcomes
- error copy/docs объясняют fallback на TOTP verify path

Verification:

- unit tests for device lookup mapping
- security tests that safe models do not expose push token/public key/device secrets

### Iteration 5. Callback signature validation

Goal:

- дать безопасную проверку signed callbacks без обязательной зависимости от ASP.NET Core

Scope:

- canonical callback verifier in `Dt1520.Authenticator.Client`
- HMAC/signature header parsing according to existing backend contract
- timestamp tolerance/replay-window inputs
- payload bytes validation without lossy string reserialization
- typed failure reasons:
  - missing signature
  - invalid format
  - timestamp outside tolerance
  - signature mismatch
  - unsupported algorithm
- no logging of raw payload by default

Exit criteria:

- backend services can validate callbacks in framework-agnostic code
- verifier docs explain raw-body requirement

Verification:

- known-good signature vector tests
- tampered payload/header/timestamp tests
- secret redaction tests

### Iteration 6. `Dt1520.Authenticator.AspNetCore`

Goal:

- сделать интеграцию с ASP.NET Core idiomatic and low-friction

Scope:

- `IServiceCollection` extension methods outside `Microsoft.Extensions.DependencyInjection` namespace
- options binding + validation
- `IHttpClientFactory` registration for typed client
- callback validation helpers for minimal APIs/controllers:
  - raw body access guidance
  - safe failure responses
  - structured logging hooks without secret leakage
- sample endpoint contracts for:
  - protected operation start
  - callback receive
  - polling/status read from integrator backend
- no business commit helper that can hide approval checks

Exit criteria:

- ASP.NET Core backend can register SDK via configuration and validate callbacks safely
- docs show where `client_secret` belongs and how to avoid browser/desktop direct secret exposure

Verification:

- DI registration tests
- options validation tests
- fake server callback tests
- logging redaction tests

### Iteration 7. `Dt1520.Authenticator.Desktop`

Goal:

- дать desktop-facing helper layer without weakening the security model

Scope:

- approval session state model:
  - `waiting`
  - `approved`
  - `denied`
  - `expired`
  - `failed`
  - `cancelled`
- polling helper against integrator backend, not directly against `DT-1520`
- cancellation/timeout helpers
- typed UI outcome helpers
- no default storage
- no `client_secret`, integration bearer token or DT-1520 direct client in desktop package

Exit criteria:

- desktop shell can model pending approval UX and TOTP fallback prompt without holding integration secret
- docs clearly state default topology: `Desktop App -> Backend -> DT-1520`

Verification:

- state transition tests
- cancellation/timeout tests
- security tests for absence of secret-bearing options

### Iteration 8. Sample backend flow and package documentation

Goal:

- сделать SDK usable by a new integrator without reading backend source

Scope:

- package README per package
- getting started guide:
  - create integration client in Admin UI
  - configure backend secret
  - register SDK
  - create challenge
  - validate callback
  - commit business operation only after `approved`
  - fallback to online TOTP verify
- package versioning and compatibility policy:
  - SemVer
  - prerelease suffixes for unstable SDKs
  - compatibility against DT-1520 API version
- troubleshooting:
  - auth failures
  - callback signature failures
  - denied/expired/failed handling
  - timeout/polling behavior
- docs app update for SDK section

Exit criteria:

- documentation-as-DoD complete for prerelease SDK
- `dotnet pack` creates packages with README/metadata

Verification:

- package README included in `.nupkg`
- docs app `npm test/build/e2e`
- `dotnet test` for all SDK packages

### Iteration 9. Prerelease closure and handoff to reference stand

Goal:

- close SDK prerelease and move next continuation point to [[Reference Desktop Backend Stand]]

Scope:

- full `lib/` verification
- package artifacts inspected locally
- security review across all packages
- update vault:
  - current state
  - implementation map
  - session note
  - docs
- define exact SDK APIs to be consumed by `rdb_stand/`

Exit criteria:

- SDK prerelease is ready for internal reference stand consumption
- no direct desktop-to-Authenticator secret path exists
- next practical continuation point is [[Reference Desktop Backend Stand]]

Verification:

- `dotnet build/test/pack` for `lib/`
- docs verification
- security checklist signed off in session note

## Documentation requirements

The NuGet track is not done until it includes:

- package README per package
- getting started guide
- configuration reference
- security notes for backend and desktop usage
- sample `Desktop + Backend` reference flow
- package versioning and compatibility policy

Related documentation tracker: [[Documentation Handoff Plan]].

## Non-goals

- replacing the REST/OpenAPI contract
- direct browser-to-Authenticator integrations
- storing integration secrets in desktop app config
- adding a new MFA factor
- making `ProjectManager` the SDK sample

## Related notes

- [[Reference Desktop Backend Stand]]
- [[Admin Client Management Follow-Up]]
- [[QR Device Onboarding Follow-Up]]
- [[Push Delivery Latency Follow-Up]]
- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
