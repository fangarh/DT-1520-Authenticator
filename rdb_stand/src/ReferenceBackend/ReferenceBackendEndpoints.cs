namespace Dt1520.Authenticator.ReferenceBackend;

public static class ReferenceBackendEndpoints
{
    public static IEndpointRouteBuilder MapReferenceBackendEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reference");

        group.MapGet("/live-readiness", GetLiveReadiness);
        group.MapPost("/operations", StartOperationAsync);
        group.MapGet("/operations/{sessionId}/status", GetStatusAsync);
        group.MapPost("/operations/{sessionId}/totp", VerifyTotpAsync);
        group.MapPost("/callbacks/dt1520", ReceiveCallbackAsync);

        return endpoints;
    }

    private static IResult GetLiveReadiness(ReferenceBackendReadinessReporter reporter)
    {
        return Results.Ok(reporter.GetReadiness());
    }

    private static async Task<IResult> StartOperationAsync(
        StartProtectedOperationRequest request,
        ProtectedOperationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.StartAsync(request, cancellationToken);
        return result.IsSuccess
            ? Results.Accepted(result.Value!.PollingPath, result.Value)
            : result.ToHttpResult();
    }

    private static async Task<IResult> GetStatusAsync(
        string sessionId,
        ProtectedOperationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var session = await coordinator.GetSessionAsync(sessionId, cancellationToken);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> VerifyTotpAsync(
        string sessionId,
        VerifyTotpFallbackRequest request,
        ProtectedOperationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.VerifyTotpAsync(sessionId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.ToHttpResult();
    }

    private static async Task<IResult> ReceiveCallbackAsync(
        HttpRequest request,
        ProtectedOperationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.ApplyCallbackAsync(request, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
    }
}
