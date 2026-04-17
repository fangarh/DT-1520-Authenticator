using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Enrollments;

namespace OtpAuth.Api.Endpoints;

public static class AdminEnrollmentCommandEndpoints
{
    public static IEndpointRouteBuilder MapAdminEnrollmentCommandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/enrollments/totp", StartTotpEnrollmentAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.EnrollmentsWritePolicy)
            .WithName("AdminStartTotpEnrollment");

        app.MapPost("/api/v1/admin/enrollments/totp/{enrollmentId:guid}/confirm", ConfirmTotpEnrollmentAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.EnrollmentsWritePolicy)
            .WithName("AdminConfirmTotpEnrollment");

        app.MapPost("/api/v1/admin/enrollments/totp/{enrollmentId:guid}/replace", ReplaceTotpEnrollmentAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.EnrollmentsWritePolicy)
            .WithName("AdminReplaceTotpEnrollment");

        app.MapPost("/api/v1/admin/enrollments/totp/{enrollmentId:guid}/revoke", RevokeTotpEnrollmentAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.EnrollmentsWritePolicy)
            .WithName("AdminRevokeTotpEnrollment");

        return app;
    }

    private static async Task<IResult> StartTotpEnrollmentAsync(
        AdminStartTotpEnrollmentHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminStartTotpEnrollmentHandler handler,
        CancellationToken cancellationToken)
    {
        var csrfError = await ValidateAntiforgeryAsync(httpContext, antiforgery);
        if (csrfError is not null)
        {
            return csrfError;
        }

        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(
            AdminTotpEnrollmentCommandRequestMapper.Map(request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                AdminStartTotpEnrollmentErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminStartTotpEnrollmentErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Application client was not found.",
                    result.ErrorMessage),
                AdminStartTotpEnrollmentErrorCode.PolicyDenied => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Enrollment creation denied by policy.",
                    result.ErrorMessage),
                AdminStartTotpEnrollmentErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Enrollment cannot be started.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid admin enrollment request.",
                    result.ErrorMessage),
            };
        }

        return Results.Created(
            $"/api/v1/admin/tenants/{request.TenantId}/users/{Uri.EscapeDataString(request.ExternalUserId.Trim())}/enrollments/totp/current",
            AdminTotpEnrollmentCommandRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> ConfirmTotpEnrollmentAsync(
        Guid enrollmentId,
        AdminConfirmTotpEnrollmentHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminConfirmTotpEnrollmentHandler handler,
        CancellationToken cancellationToken)
    {
        var csrfError = await ValidateAntiforgeryAsync(httpContext, antiforgery);
        if (csrfError is not null)
        {
            return csrfError;
        }

        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(
            AdminTotpEnrollmentCommandRequestMapper.Map(enrollmentId, request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                ConfirmTotpEnrollmentErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                ConfirmTotpEnrollmentErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Enrollment was not found.",
                    result.ErrorMessage),
                ConfirmTotpEnrollmentErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Enrollment cannot be confirmed.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid enrollment confirmation request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(AdminTotpEnrollmentCommandRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> ReplaceTotpEnrollmentAsync(
        Guid enrollmentId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminReplaceTotpEnrollmentHandler handler,
        CancellationToken cancellationToken)
    {
        var csrfError = await ValidateAntiforgeryAsync(httpContext, antiforgery);
        if (csrfError is not null)
        {
            return csrfError;
        }

        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(enrollmentId, adminContext!, cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                ReplaceTotpEnrollmentErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                ReplaceTotpEnrollmentErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Enrollment was not found.",
                    result.ErrorMessage),
                ReplaceTotpEnrollmentErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Enrollment cannot be replaced.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid enrollment replace request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(AdminTotpEnrollmentCommandRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> RevokeTotpEnrollmentAsync(
        Guid enrollmentId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminRevokeTotpEnrollmentHandler handler,
        CancellationToken cancellationToken)
    {
        var csrfError = await ValidateAntiforgeryAsync(httpContext, antiforgery);
        if (csrfError is not null)
        {
            return csrfError;
        }

        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(enrollmentId, adminContext!, cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                RevokeTotpEnrollmentErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                RevokeTotpEnrollmentErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Enrollment was not found.",
                    result.ErrorMessage),
                RevokeTotpEnrollmentErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Enrollment cannot be revoked.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid enrollment revoke request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(AdminTotpEnrollmentCommandRequestMapper.MapResponse(result.Enrollment));
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

    private static async Task<IResult?> ValidateAntiforgeryAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid anti-forgery token.",
                "A valid anti-forgery token is required.");
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
