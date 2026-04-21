using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminDeliveryStatusEndpoints
{
    public static IEndpointRouteBuilder MapAdminDeliveryStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/admin/tenants/{tenantId:guid}/delivery-statuses",
                ListAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.WebhooksReadPolicy)
            .WithName("AdminListDeliveryStatuses");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        Guid? applicationClientId,
        string? channel,
        string? status,
        int? limit,
        HttpContext httpContext,
        AdminListDeliveryStatusesHandler handler,
        CancellationToken cancellationToken)
    {
        if (!AdminDeliveryStatusRequestMapper.TryMap(
                tenantId,
                applicationClientId,
                channel,
                status,
                limit,
                out var request,
                out var validationError))
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid delivery status filter.",
                validationError);
        }

        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(request!, adminContext!, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                AdminListDeliveryStatusesErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminListDeliveryStatusesErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Application client was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid delivery status lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(result.Deliveries.Select(AdminDeliveryStatusRequestMapper.MapResponse));
    }

    private static AdminContext? GetAdminContextOrProblem(HttpContext httpContext, out IResult? authError)
    {
        try
        {
            authError = null;
            return httpContext.GetRequiredAdminContext();
        }
        catch (InvalidOperationException)
        {
            authError = CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                "Authenticated principal is missing admin session claims.");
            return null;
        }
    }

    private static IResult CreateProblem(int statusCode, string title, string? detail)
    {
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            type: $"https://otpauth.dev/problems/{statusCode}");
    }
}
