using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminCombinedOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapAdminCombinedOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/combined-onboarding-packages", CreateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.CombinedOnboardingWritePolicy)
            .WithName("AdminCreateCombinedOnboardingPackage");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        AdminCreateCombinedOnboardingPackageHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminCreateCombinedOnboardingPackageHandler handler,
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
            AdminCombinedOnboardingRequestMapper.Map(request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess ||
            result.DeviceArtifact is null ||
            result.ActivationPayload is null ||
            result.TotpEnrollment is null)
        {
            return result.ErrorCode switch
            {
                AdminCreateCombinedOnboardingPackageErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminCreateCombinedOnboardingPackageErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Application client was not found.",
                    result.ErrorMessage),
                AdminCreateCombinedOnboardingPackageErrorCode.PolicyDenied => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Combined onboarding denied by policy.",
                    result.ErrorMessage),
                AdminCreateCombinedOnboardingPackageErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Combined onboarding package cannot be created.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid combined onboarding request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Created(
            $"/api/v1/admin/tenants/{result.DeviceArtifact.TenantId:D}/device-onboarding-artifacts/{result.DeviceArtifact.ActivationCodeId:D}",
            AdminCombinedOnboardingRequestMapper.MapResponse(
                result.DeviceArtifact,
                result.ActivationPayload,
                result.TotpEnrollment));
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
