# Dt1520.Authenticator.Desktop

Desktop approval session helper package for applications that use an integrator backend.

Implemented responsibilities:

- Desktop approval session state.
- Polling helpers against the integrator backend.
- Typed UI outcomes for approved, denied, expired, failed and cancelled states.
- Local timeout and cancellation outcomes for desktop waiting UI.

This package must not contain DT-1520 `client_secret`, integration bearer token settings or a direct DT-1520 API client. The default topology remains `Desktop App -> Integrator Backend -> DT-1520 Authenticator`.

## Install

```powershell
dotnet add package Dt1520.Authenticator.Desktop --prerelease
```

Current prerelease version: `0.1.0-alpha.1`.

## Runtime model

`DesktopApprovalPoller` accepts a caller-provided `HttpClient`, `DesktopApprovalPollingOptions` with an integrator backend base URL and a `DesktopApprovalSession` with a relative polling path.

The desktop app polls its own backend. The backend remains responsible for storing DT-1520 integration credentials, calling DT-1520, validating callbacks and applying protected business changes only after approval.

Do not put integration `client_secret`, bearer tokens, callback signing secrets, QR activation payloads or mobile device tokens in desktop app configuration or logs.

## Polling example

```csharp
var poller = new DesktopApprovalPoller(
    httpClient,
    new DesktopApprovalPollingOptions
    {
        BackendBaseUrl = new Uri("https://app-backend.example.test/"),
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2),
    });

var outcome = await poller.PollUntilCompletedAsync(new DesktopApprovalSession
{
    SessionId = operationId,
    PollingPath = $"/protected-operations/{operationId}",
    Status = DesktopApprovalSessionStatus.Waiting,
    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
}, cancellationToken);

if (outcome.Kind == DesktopApprovalOutcomeKind.Approved)
{
    // Continue UI flow after the backend has committed the protected change.
}
```

The polling path must be relative and stay under the configured integrator backend origin. Treat denied, expired, failed, cancelled and timed-out outcomes as non-approval in desktop UI.

## Troubleshooting

- Local timeout: show a retry or fallback path; do not assume DT-1520 approval.
- Backend polling failure: inspect your own backend status endpoint and keep raw backend error bodies out of desktop logs.
- Missing approval: confirm that the backend received and validated the DT-1520 callback, then updated the desktop-visible operation status.
