# DT-1520 Authenticator .NET SDK Prerelease Handoff

## Status

`0.1.0-alpha.1` is ready for internal reference stand consumption after local build, test, pack and package inspection pass.

Packages:

- `Dt1520.Authenticator.Client`
- `Dt1520.Authenticator.AspNetCore`
- `Dt1520.Authenticator.Desktop`

Target framework:

- `net8.0`

## Verification Gate

Run from `lib/` before handing packages to `rdb_stand/`:

```powershell
dotnet restore .\Dt1520.Authenticator.slnx
dotnet build .\Dt1520.Authenticator.slnx --no-restore -maxcpucount:1
dotnet test .\Dt1520.Authenticator.slnx --no-build -maxcpucount:1
dotnet build .\Dt1520.Authenticator.slnx --no-restore --configuration Release -maxcpucount:1
dotnet test .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1
dotnet pack .\Dt1520.Authenticator.slnx --no-build --configuration Release -maxcpucount:1
```

Inspect the generated `.nupkg` files under `lib/artifacts/bin/*/Release/` and confirm:

- root `README.md` exists in every package;
- `lib/net8.0/*.xml` documentation exists in every package;
- `.snupkg` symbol packages are produced;
- package metadata keeps `PackageId`, `Version`, `Authors`, `Description`, `PackageTags`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl` and `RepositoryType`;
- package content contains no real credentials, bearer tokens, callback payloads, QR activation payloads, mobile device tokens or private URLs with embedded credentials.

## APIs For Reference Stand

The first `rdb_stand/` implementation should consume only these SDK surfaces:

- `AddDt1520Authenticator(...)` from `Dt1520.Authenticator.AspNetCore`;
- `Dt1520AuthenticatorClient.CreateChallengeAsync(...)`;
- `Dt1520AuthenticatorClient.GetChallengeAsync(...)`;
- `Dt1520AuthenticatorClient.VerifyTotpAsync(...)`;
- `Dt1520AuthenticatorClient.ListDevicesForRoutingAsync(...)`;
- `Dt1520AuthenticatorClient.SelectSinglePushDeviceAsync(...)`;
- `CallbackSignatureVerifier` or `Dt1520AuthenticatorCallbackValidator`;
- `DesktopApprovalSession`;
- `DesktopApprovalPoller`;
- `DesktopApprovalOutcome`.

The reference backend owns `clientId`, `clientSecret`, callback signing secret and all calls to DT-1520 Authenticator. The desktop shell owns only a reference backend URL and a relative approval status path.

## Security Checklist

- Desktop must not contain `client_secret`, bearer tokens, callback signing secrets or direct DT-1520 base URL settings.
- Browser or desktop clients must not call DT-1520 Authenticator directly in the default flow.
- Protected business state is committed only after a validated `approved` result.
- `denied`, `expired`, `failed`, timeout and cancellation are non-approval outcomes.
- Callback validation uses original raw HTTP request body bytes before JSON parsing or serialization.
- Online TOTP fallback uses `VerifyTotpAsync` against an active challenge so centralized replay defense, audit and rate limiting remain active.
- Device routing models expose only safe metadata and never expose push tokens, public keys, installation IDs or device tokens.
- SDK logs and docs must not echo raw callback payloads, TOTP codes, bearer tokens, client secrets, QR activation payloads or mobile device tokens.

## Handoff To `rdb_stand/`

Next track:

```text
Desktop App -> Reference Backend -> DT-1520 Authenticator -> Android App
```

The first stand slice should implement:

- reference backend package references through local prerelease packages or project references;
- backend start endpoint that creates a challenge through `CreateChallengeAsync`;
- backend callback endpoint that validates `X-OTPAuth-Signature` over original request body bytes;
- backend status endpoint consumed by `DesktopApprovalPoller`;
- desktop pending approval UX based on `DesktopApprovalSession` and `DesktopApprovalOutcome`;
- online TOTP fallback that calls the backend, then `VerifyTotpAsync`;
- latency timestamps from desktop submit through Android approve or deny terminal state.
