# Dt1520.Authenticator.AspNetCore

ASP.NET Core integration helper package for DT-1520 Authenticator backend integrations.

Implemented responsibilities:

- `IServiceCollection` registration through `AddDt1520Authenticator(...)`.
- Options binding and validation for backend-hosted Authenticator configuration.
- Named `IHttpClientFactory` wiring for `Dt1520.Authenticator.Client`.
- Callback request helpers that validate the original request body bytes with `EnableBuffering()` and reset the request body stream.
- Sanitized callback failure results for minimal API or controller code.

This package must keep `client_secret` in server-side configuration and must not hide the business rule that protected operations are committed only after an approved DT-1520 result.

## Install

```powershell
dotnet add package Dt1520.Authenticator.AspNetCore --prerelease
```

Current prerelease version: `0.1.0-alpha.1`.

## Configuration shape

Use a server-side configuration section such as:

```json
{
  "Dt1520Authenticator": {
    "BaseUrl": "https://auth.example.test/",
    "ClientId": "integration-client-id",
    "ClientSecret": "<server-side secret>",
    "CallbackSigningSecret": "<server-side callback secret>"
  }
}
```

Register the SDK from ASP.NET Core startup code:

```csharp
builder.Services
    .AddDt1520Authenticator(builder.Configuration.GetSection("Dt1520Authenticator"))
    .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
```

The returned `IHttpClientBuilder` is optional customization surface for proxy, resilience or handler configuration. Do not log `ClientSecret`, `CallbackSigningSecret`, bearer tokens or raw callback payloads.

## Callback validation

`Dt1520AuthenticatorCallbackValidator.ValidateAsync(HttpRequest)` validates `X-OTPAuth-Signature` against the original body bytes. The helper buffers the request body, reads bytes exactly as received, resets the body position and returns a sanitized result.

Application code must still parse the callback payload and apply protected business changes only after it has confirmed the DT-1520 challenge result is approved.

## Minimal API protected operation flow

The recommended backend shape is:

1. Receive a sensitive operation draft from your authenticated user.
2. Store the draft in your backend with a pending status.
3. Create a DT-1520 challenge through `Dt1520AuthenticatorClient`.
4. Return a pending operation id and a backend-owned status URL to the browser or desktop app.
5. Validate the DT-1520 callback signature through `Dt1520AuthenticatorCallbackValidator`.
6. Commit the pending draft exactly once only after an approved callback or a confirmed approved challenge read.
7. Mark denied, expired and failed outcomes without committing the protected change.

```csharp
builder.Services.AddDt1520Authenticator(
    builder.Configuration.GetSection("Dt1520Authenticator"));

app.MapPost("/protected-operations", async (
    Dt1520AuthenticatorClient authenticator,
    ProtectedOperationDraft draft,
    CancellationToken cancellationToken) =>
{
    var challenge = await authenticator.CreateChallengeAsync(
        draft.ToChallengeRequest(),
        cancellationToken);

    return challenge.IsSuccess
        ? Results.Accepted($"/protected-operations/{draft.OperationId}", new
        {
            draft.OperationId,
            challengeId = challenge.Value!.Id,
            statusPath = $"/protected-operations/{draft.OperationId}",
        })
        : Results.Problem(challenge.Error!.Title, statusCode: challenge.Error.StatusCode);
});

app.MapPost("/dt1520/challenge-callbacks", async (
    HttpRequest request,
    Dt1520AuthenticatorCallbackValidator callbackValidator,
    CancellationToken cancellationToken) =>
{
    var signature = await callbackValidator.ValidateAsync(request, cancellationToken);
    if (!signature.IsValid)
    {
        return signature.ToFailureHttpResult();
    }

    var callback = await request.ReadFromJsonAsync<ChallengeCallbackEnvelope>(
        cancellationToken: cancellationToken);

    if (callback?.Status == ChallengeStatus.Approved)
    {
        // Commit the pending operation by callback.CorrelationId exactly once.
        return Results.NoContent();
    }

    // Store terminal non-approval status for polling clients.
    return Results.Accepted();
});
```

Keep callback endpoints server-to-server. Browsers and desktop clients should poll your backend for operation status; they should not validate DT-1520 callbacks or hold DT-1520 integration credentials.

## Troubleshooting

- Startup validation fails: verify `BaseUrl`, `ClientId`, `ClientSecret` and `CallbackSigningSecret` are present in server-side configuration.
- Callback validation fails: make sure validation runs before body-consuming middleware loses or changes the original body bytes.
- Protected change applied too early: keep business commit code outside the start endpoint and run it only after an approved challenge state.
