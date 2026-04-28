# ASP.NET Core Protected Operation Sample Flow

This sample documents the intended backend shape for a sensitive operation that requires DT-1520 step-up approval before the business change is committed.

It is intentionally a flow sample, not a runnable service. Storage, authentication and domain-specific commit logic belong to the integrating backend.

## Boundaries

- Store `clientSecret` and `callbackSigningSecret` only in backend secret storage.
- The browser or desktop app talks only to the integrator backend.
- The integrator backend calls DT-1520 through `Dt1520AuthenticatorClient`.
- DT-1520 callbacks are validated over the original HTTP request body bytes.
- The protected business change is committed exactly once only after `approved`.

## Configuration

```json
{
  "Dt1520Authenticator": {
    "BaseUrl": "https://auth.example.test/",
    "ClientId": "integration-client-id",
    "ClientSecret": "<backend-secret-store>",
    "CallbackSigningSecret": "<backend-secret-store>"
  }
}
```

## Startup

```csharp
using Dt1520.Authenticator.AspNetCore;
using Dt1520.Authenticator.Client;

builder.Services.AddDt1520Authenticator(
    builder.Configuration.GetSection("Dt1520Authenticator"));
```

## Start a protected operation

```csharp
app.MapPost("/protected-operations", async (
    Dt1520AuthenticatorClient authenticator,
    ProtectedOperationDraft draft,
    IProtectedOperationStore store,
    CancellationToken cancellationToken) =>
{
    await store.SavePendingAsync(draft, cancellationToken);

    var challenge = await authenticator.CreateChallengeAsync(new CreateChallengeRequest
    {
        TenantId = draft.TenantId,
        ApplicationClientId = draft.ApplicationClientId,
        Subject = new ChallengeSubject { ExternalUserId = draft.ExternalUserId },
        Operation = new ChallengeOperation
        {
            Type = ChallengeOperationType.StepUp,
            DisplayName = draft.DisplayName,
        },
        PreferredFactors = [ChallengeFactorType.Push, ChallengeFactorType.Totp],
        Callback = new ChallengeCallbackRegistration
        {
            Url = new Uri("https://backend.example.test/dt1520/callbacks/challenges"),
        },
        CorrelationId = draft.OperationId,
        IdempotencyKey = draft.OperationId,
    }, cancellationToken);

    if (!challenge.IsSuccess)
    {
        await store.MarkFailedAsync(draft.OperationId, cancellationToken);
        return Results.Problem(challenge.Error!.Title, statusCode: challenge.Error.StatusCode);
    }

    await store.BindChallengeAsync(draft.OperationId, challenge.Value!.Id, cancellationToken);
    return Results.Accepted($"/protected-operations/{draft.OperationId}", new
    {
        draft.OperationId,
        challengeId = challenge.Value.Id,
        statusPath = $"/protected-operations/{draft.OperationId}",
    });
});
```

## Receive DT-1520 callback

```csharp
app.MapPost("/dt1520/callbacks/challenges", async (
    HttpRequest request,
    Dt1520AuthenticatorCallbackValidator callbackValidator,
    IProtectedOperationStore store,
    CancellationToken cancellationToken) =>
{
    var validation = await callbackValidator.ValidateAsync(request, cancellationToken);
    if (!validation.IsValid)
    {
        return validation.ToFailureHttpResult();
    }

    var callback = await request.ReadFromJsonAsync<ChallengeCallbackEnvelope>(
        cancellationToken: cancellationToken);
    if (callback is null)
    {
        return Results.BadRequest();
    }

    if (callback.Status == ChallengeStatus.Approved)
    {
        await store.CommitApprovedAsync(callback.CorrelationId, cancellationToken);
        return Results.NoContent();
    }

    await store.MarkTerminalNonApprovalAsync(
        callback.CorrelationId,
        callback.Status,
        cancellationToken);
    return Results.Accepted();
});
```

## Status endpoint for browser or desktop clients

```csharp
app.MapGet("/protected-operations/{operationId}", async (
    string operationId,
    IProtectedOperationStore store,
    CancellationToken cancellationToken) =>
{
    var status = await store.GetStatusAsync(operationId, cancellationToken);
    return status is null ? Results.NotFound() : Results.Ok(status);
});
```

Desktop apps can poll this endpoint with `DesktopApprovalPoller`. The desktop app never receives DT-1520 integration credentials.

## Online TOTP fallback

When backend policy allows online TOTP fallback, collect the code in the integrator UI and send it to the backend. The backend calls DT-1520:

```csharp
var result = await authenticator.VerifyTotpAsync(
    challengeId,
    new VerifyTotpRequest { Code = codeFromUser },
    cancellationToken);

if (result.IsSuccess && result.Value!.Status == ChallengeStatus.Approved)
{
    await store.CommitApprovedAsync(operationId, cancellationToken);
}
```

Never log the submitted TOTP code. Failed, denied or expired results must not apply the protected business change.

## Troubleshooting map

- Auth failures: check integration client status, scopes and latest rotated secret in Admin UI.
- Callback failures: verify `X-OTPAuth-Signature` and raw body preservation before JSON parsing.
- Polling timeouts: keep the operation pending or failed according to backend policy; do not infer approval.
- Ambiguous push devices: prompt for device selection or use TOTP fallback only if policy permits it.
