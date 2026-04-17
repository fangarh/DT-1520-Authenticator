using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Enrollments;

namespace OtpAuth.Api.Endpoints;

public static class AdminEnrollmentReadEndpoints
{
    public static IEndpointRouteBuilder MapAdminEnrollmentReadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/admin/tenants/{tenantId:guid}/users/{externalUserId}/enrollments/totp/current",
                GetCurrentTotpEnrollmentAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.EnrollmentsReadPolicy)
            .WithName("GetCurrentTotpEnrollmentForAdmin");

        return app;
    }

    private static async Task<IResult> GetCurrentTotpEnrollmentAsync(
        Guid tenantId,
        string externalUserId,
        HttpContext httpContext,
        GetCurrentTotpEnrollmentForAdminHandler handler,
        CancellationToken cancellationToken)
    {
        Application.Administration.AdminContext adminContext;
        try
        {
            adminContext = httpContext.GetRequiredAdminContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                "Authenticated principal is missing admin session claims.");
        }

        var result = await handler.HandleAsync(
            tenantId,
            externalUserId,
            adminContext,
            cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                GetCurrentTotpEnrollmentForAdminErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                GetCurrentTotpEnrollmentForAdminErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Current enrollment was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid current enrollment lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminTotpEnrollmentRequestMapper.MapResponse(result.Enrollment));
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
