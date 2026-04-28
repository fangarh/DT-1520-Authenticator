# Official Dotnet Integration SDK Context Reset Prompt

## Purpose

Use this prompt to continue the SDK productization track after context reset.

## Prompt

Продолжай `Official .NET SDK` track с `Iteration 9 - prerelease closure and handoff to Reference Desktop Backend Stand`.

Сначала прочитай:

1. `OTP/00 - Start Here.md`
2. `OTP/01 - Current State.md`
3. `OTP/02 - Decision Index.md`
4. `OTP/Agent/Implementation Map.md`
5. `OTP/Delivery/Official Dotnet Integration SDK.md`
6. `OTP/Delivery/Reference Desktop Backend Stand.md`
7. `OTP/Delivery/Documentation Handoff Plan.md`
8. `OTP/Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand.md`
9. `lib/README.md`
10. `lib/Dt1520.Authenticator.slnx`

Iteration 0 уже закрыта как contract preflight:

- package ids: `Dt1520.Authenticator.Client`, `Dt1520.Authenticator.AspNetCore`, `Dt1520.Authenticator.Desktop`;
- first target framework: `net8.0`;
- no default `netstandard2.0` until real consumer need appears;
- no default `net10.0` until repo/runtime baseline creates concrete value;
- public APIs are async-first, nullable-enabled, XML-documented and result/error-oriented for expected backend `ProblemDetails`;
- desktop package must not contain `client_secret`, integration bearer token or direct `DT-1520` client configuration;
- callback signature validation must use original payload bytes, not JSON reserialization.

Iteration 1 уже закрыта как repository/package scaffold:

- `lib/Dt1520.Authenticator.slnx`;
- package projects under `lib/src/Dt1520.Authenticator.Client|AspNetCore|Desktop`;
- test projects under `lib/tests/Dt1520.Authenticator.Client.Tests|AspNetCore.Tests|Desktop.Tests`;
- shared MSBuild settings in `lib/Directory.Build.props` and `lib/Directory.Build.targets`;
- package README files included through `PackageReadmeFile`;
- scaffold xUnit tests for package metadata and security-boundary wording;
- verification passed: restore/build/test (`6/6`), Release build and Release pack for all three packages.

Iteration 2 уже закрыта как `Client` core HTTP/token/problem foundation:

- implemented `Dt1520AuthenticatorClientOptions`, `Dt1520AuthenticatorClientCredentials`, `Dt1520AuthenticatorClient`, `Dt1520AuthenticatorResult<T>`, `Dt1520AuthenticatorError` and `Dt1520AuthenticatorAccessToken`;
- `AuthenticateAsync` calls `/oauth2/token` with OAuth 2.0 `client_credentials` form data and caches bearer tokens until expiry minus skew;
- internal authorized JSON plumbing attaches bearer auth only under configured DT-1520 base URL;
- `ProblemDetails` maps to stable SDK error categories and separates cancellation/timeout/transport failures;
- secret-bearing `ToString` paths redact `client_secret` and bearer tokens;
- verification passed: Debug build/test (`24/24`), Release build/test (`24/24`) and Release pack.

Iteration 3 уже закрыта как challenge lifecycle and online `TOTP` typed client:

- implemented public models `CreateChallengeRequest`, `ChallengeSubject`, `ChallengeOperation`, `ChallengeCallbackRegistration`, `ChallengeResponse`, `VerifyTotpRequest`, `ChallengeFactorType`, `ChallengeOperationType`, `ChallengeStatus`;
- `Dt1520AuthenticatorClient` now exposes `CreateChallengeAsync`, `GetChallengeAsync` and `VerifyTotpAsync`;
- request JSON maps to existing backend/OpenAPI contract for `/api/v1/challenges`, `/api/v1/challenges/{id}` and `/api/v1/challenges/{id}/verify-totp`;
- `CreateChallengeRequest.IdempotencyKey` is sent as `Idempotency-Key` header and excluded from JSON body;
- validation rejects malformed required fields and invalid six-digit TOTP codes before network calls;
- `VerifyTotpRequest.ToString()` redacts codes, and challenge request `ToString()` avoids callback/user echoing;
- verification passed: Debug build/test (`34/34`, client `30/30`), Release build/test and Release pack.

Iteration 4 уже закрыта как device lookup and push workflow helpers:

- implemented `ListDevicesForRoutingAsync(externalUserId, pushCapableOnly)` over `GET /api/v1/devices`;
- implemented `SelectSinglePushDeviceAsync(externalUserId)` and `PushDeviceSelectionResult`;
- added public safe models `DeviceRoutingCandidate`, `DevicePlatform`, `DeviceStatus`, `DeviceAttestationStatus`, `PushDeviceSelectionStatus`, `PushChallengeOutcome` and `PushChallengeOutcomeKind`;
- no push token, public key, installation id, device access/refresh token, QR activation payload or desktop secret-bearing config was introduced;
- verification passed: Debug/Release build, Debug/Release tests (`47/47`) and Release pack.

Iteration 5 уже закрыта как framework-agnostic callback signature validation:

- implemented `CallbackSignatureVerifier`, `CallbackSignatureVerifierOptions`, `CallbackSignatureVerificationResult` and `CallbackSignatureVerificationFailureKind` in `Dt1520.Authenticator.Client`;
- verifier validates `X-OTPAuth-Signature` values in the existing backend format `sha256=<hex>`;
- HMAC is computed with `HMACSHA256` over original raw request body bytes and compared through `CryptographicOperations.FixedTimeEquals`;
- optional timestamp header input supports replay-window tolerance without requiring a timestamp header in the current backend contract;
- failure reasons cover missing signature, invalid format, unsupported algorithm, timestamp outside tolerance and signature mismatch;
- secret and payload redaction is covered by tests.

Iteration 6 уже закрыта как `ASP.NET Core` DI/options/callback helper package:

- implemented `AddDt1520Authenticator(...)` overloads for configuration-bound and code-based backend options;
- registered `Dt1520AuthenticatorClient` through named `IHttpClientFactory` client `Dt1520.Authenticator`;
- added `Dt1520AuthenticatorAspNetCoreOptions` with validation for base URL, integration credentials, callback signing secret, timeouts/skew, product tokens and callback body size;
- added `Dt1520AuthenticatorCallbackValidator`, which reads original `HttpRequest.Body` bytes with `EnableBuffering`, resets the body stream and validates `X-OTPAuth-Signature` through the framework-agnostic verifier;
- callback validation results expose only stable failure kinds, body length and sanitized HTTP failure response helpers;
- callback signing secret, integration client secret, bearer tokens and raw callback payload are not logged or stored in result objects;
- no business commit helper was added.

Iteration 6 verification passed:

- Context7 `/dotnet/aspnetcore.docs` was checked for options binding/validation, `IHttpClientFactory` and `EnableBuffering`;
- Debug build/test passed (`70/70`);
- Release build/test passed (`70/70`);
- Release pack passed and `.nupkg/.snupkg` artifacts exist for all three packages.

Iteration 7 уже закрыта как `Desktop` helper package:

- implemented `DesktopApprovalSession`, `DesktopApprovalSessionStatus`, `DesktopApprovalPollingOptions`, `DesktopApprovalPoller`, `DesktopApprovalOutcome` and `DesktopApprovalOutcomeKind`;
- poller works against integrator backend base URL + relative polling path through caller-provided `HttpClient`;
- no `client_secret`, bearer token, callback signing secret, direct DT-1520 base URL or `Dt1520.Authenticator.Client` dependency was added to Desktop package;
- outcomes cover approved, denied, expired, failed, cancelled and local timeout;
- response parsing is size-limited, absolute polling paths are rejected, raw backend failure bodies are not exposed.

Iteration 7 verification passed:

- Context7 `/dotnet/docs` checked for async polling/cancellation patterns;
- Debug build/test passed (`82/82`, desktop `14/14`);
- Release build/test passed (`82/82`);
- Release pack passed for the Desktop package artifacts;
- docs verification passed (`npm test`, `npm run build`, `npm run test:e2e`).

Iteration 8 expected output:

- completed on `2026-04-27`;
- package README files and docs app SDK section now contain usable getting-started guidance;
- `lib/samples/aspnetcore-protected-operation/README.md` documents create challenge, callback validation, polling/status read and online TOTP fallback;
- backend-only `client_secret` and callback signing secret boundaries are preserved;
- verification passed: SDK Debug/Release build/test (`84/84`), Release pack, `.nupkg` README/XML inspection, docs `npm test`, `npm run build`, `npm run test:e2e`.

Iteration 9 expected output:

- perform prerelease closure security review across Client, AspNetCore and Desktop package surfaces;
- run full `lib/` build/test/pack and inspect package artifacts;
- define exact SDK APIs to be consumed by `rdb_stand/`;
- update vault/current state/implementation map/session note;
- move next continuation point to `Reference Desktop Backend Stand`.

Historical Iteration 5 expected output:

- inspect existing callback signature backend contract before adding public API;
- implement framework-agnostic callback verifier in `Dt1520.Authenticator.Client`;
- parse existing signature/timestamp headers according to backend contract;
- validate original raw payload bytes without JSON reserialization;
- expose typed failure reasons for missing signature, invalid format, timestamp outside tolerance, signature mismatch and unsupported algorithm;
- keep callback secret and raw payload out of `ToString`, errors and default docs/logging examples.

Iteration 5 verification expectations:

- use Context7 for current `.NET` crypto/time comparison docs if implementation details depend on framework/library behavior;
- run `dotnet build/test/pack` for `lib/`;
- add known-good signature vector tests, tampered payload/header/timestamp tests and secret redaction tests;
- perform security review for raw-body validation, timing-safe comparison and secret/payload redaction.

Historical Iteration 4 expected output:

- inspect existing integration-visible device lookup/routing backend contract and OpenAPI note before adding public API;
- implement typed device lookup/routing candidate models only for safe metadata;
- add client methods/helpers for push-capable active device selection where backend contract allows it;
- preserve backend authority: SDK must not expose push tokens/public keys/device secrets and must not make local trust decisions;
- add tests for request/response mapping, no-secret models, expected no-device/ambiguous-device outcomes and error mapping.

Verification expectations for Iteration 4:

- use Context7 for current `.NET` docs if implementation details depend on framework/library behavior;
- run `dotnet build/test/pack` for `lib/`;
- add focused unit tests for every new public behavior;
- perform security review for safe device metadata, bearer auth scope and absence of mobile secret leakage.

Do not start `Reference Desktop Backend Stand` until SDK prerelease closure moves the continuation point there.
