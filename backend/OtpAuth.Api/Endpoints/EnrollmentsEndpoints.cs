using OtpAuth.Api.Authentication;
using OtpAuth.Api.Enrollments;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Endpoints;

public static class EnrollmentsEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/enrollments/totp/{enrollmentId:guid}", GetTotpEnrollmentAsync)
            .RequireAuthorization("EnrollmentsWrite")
            .WithName("GetTotpEnrollment");

        app.MapPost("/api/v1/enrollments/totp/{enrollmentId:guid}/replace", ReplaceTotpEnrollmentAsync)
            .RequireAuthorization("EnrollmentsWrite")
            .WithName("ReplaceTotpEnrollment");

        app.MapPost("/api/v1/enrollments/totp/{enrollmentId:guid}/revoke", RevokeTotpEnrollmentAsync)
            .RequireAuthorization("EnrollmentsWrite")
            .WithName("RevokeTotpEnrollment");

        app.MapPost("/api/v1/enrollments/totp", StartTotpEnrollmentAsync)
            .RequireAuthorization("EnrollmentsWrite")
            .WithName("StartTotpEnrollment");

        app.MapPost("/api/v1/enrollments/totp/{enrollmentId:guid}/confirm", ConfirmTotpEnrollmentAsync)
            .RequireAuthorization("EnrollmentsWrite")
            .WithName("ConfirmTotpEnrollment");

        return app;
    }

    private static async Task<IResult> GetTotpEnrollmentAsync(
        Guid enrollmentId,
        GetTotpEnrollmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IntegrationClientContext clientContext;
        try
        {
            clientContext = httpContext.GetRequiredIntegrationClientContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(StatusCodes.Status401Unauthorized, "Authentication failed.", "Authenticated principal is missing integration client claims.");
        }

        var result = await handler.HandleAsync(enrollmentId, clientContext, cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                GetTotpEnrollmentErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                GetTotpEnrollmentErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Enrollment was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid enrollment request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(TotpEnrollmentRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> ReplaceTotpEnrollmentAsync(
        Guid enrollmentId,
        ReplaceTotpEnrollmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IntegrationClientContext clientContext;
        try
        {
            clientContext = httpContext.GetRequiredIntegrationClientContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(StatusCodes.Status401Unauthorized, "Authentication failed.", "Authenticated principal is missing integration client claims.");
        }

        var result = await handler.HandleAsync(enrollmentId, clientContext, cancellationToken);
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

        return Results.Ok(TotpEnrollmentRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> RevokeTotpEnrollmentAsync(
        Guid enrollmentId,
        RevokeTotpEnrollmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IntegrationClientContext clientContext;
        try
        {
            clientContext = httpContext.GetRequiredIntegrationClientContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(StatusCodes.Status401Unauthorized, "Authentication failed.", "Authenticated principal is missing integration client claims.");
        }

        var result = await handler.HandleAsync(enrollmentId, clientContext, cancellationToken);
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

        return Results.Ok(TotpEnrollmentRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> StartTotpEnrollmentAsync(
        StartTotpEnrollmentHttpRequest request,
        StartTotpEnrollmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IntegrationClientContext clientContext;
        try
        {
            clientContext = httpContext.GetRequiredIntegrationClientContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(StatusCodes.Status401Unauthorized, "Authentication failed.", "Authenticated principal is missing integration client claims.");
        }

        var result = await handler.HandleAsync(
            TotpEnrollmentRequestMapper.Map(request),
            clientContext,
            cancellationToken);
        if (!result.IsSuccess || result.Enrollment is null)
        {
            return result.ErrorCode switch
            {
                StartTotpEnrollmentErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                StartTotpEnrollmentErrorCode.PolicyDenied => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Enrollment creation denied by policy.",
                    result.ErrorMessage),
                StartTotpEnrollmentErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Enrollment cannot be started.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid enrollment request.",
                    result.ErrorMessage),
            };
        }

        return Results.Created(
            $"/api/v1/enrollments/totp/{result.Enrollment.EnrollmentId}",
            TotpEnrollmentRequestMapper.MapResponse(result.Enrollment));
    }

    private static async Task<IResult> ConfirmTotpEnrollmentAsync(
        Guid enrollmentId,
        ConfirmTotpEnrollmentHttpRequest request,
        ConfirmTotpEnrollmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IntegrationClientContext clientContext;
        try
        {
            clientContext = httpContext.GetRequiredIntegrationClientContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(StatusCodes.Status401Unauthorized, "Authentication failed.", "Authenticated principal is missing integration client claims.");
        }

        var result = await handler.HandleAsync(
            TotpEnrollmentRequestMapper.Map(enrollmentId, request),
            clientContext,
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

        return Results.Ok(TotpEnrollmentRequestMapper.MapResponse(result.Enrollment));
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
