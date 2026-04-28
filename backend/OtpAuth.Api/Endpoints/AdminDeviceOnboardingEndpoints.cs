using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminDeviceOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapAdminDeviceOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/admin/tenants/{tenantId:guid}/device-onboarding-artifacts",
                ListAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.DevicesReadPolicy)
            .WithName("AdminListDeviceOnboardingArtifacts");

        app.MapPost("/api/v1/admin/device-onboarding-artifacts", CreateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.DevicesWritePolicy)
            .WithName("AdminCreateDeviceOnboardingArtifact");

        app.MapPost(
                "/api/v1/admin/tenants/{tenantId:guid}/device-onboarding-artifacts/{activationCodeId:guid}/revoke",
                RevokeAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.DevicesWritePolicy)
            .WithName("AdminRevokeDeviceOnboardingArtifact");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        string? externalUserId,
        Guid? applicationClientId,
        string? status,
        int? limit,
        HttpContext httpContext,
        AdminListDeviceOnboardingArtifactsHandler handler,
        CancellationToken cancellationToken)
    {
        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var parsedStatus = AdminDeviceOnboardingRequestMapper.ParseStatus(status);
        if (!string.IsNullOrWhiteSpace(status) && parsedStatus is null)
        {
            return CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid device onboarding lookup request.",
                "Status must be pending, consumed, expired or revoked.");
        }

        var result = await handler.HandleAsync(
            new AdminDeviceOnboardingListRequest
            {
                TenantId = tenantId,
                ExternalUserId = externalUserId,
                ApplicationClientId = applicationClientId,
                Status = parsedStatus,
                Limit = limit ?? 50,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                AdminListDeviceOnboardingArtifactsErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminListDeviceOnboardingArtifactsErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Device onboarding artifacts were not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device onboarding lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(result.Artifacts.Select(AdminDeviceOnboardingRequestMapper.MapResponse));
    }

    private static async Task<IResult> CreateAsync(
        AdminCreateDeviceOnboardingArtifactHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminCreateDeviceOnboardingArtifactHandler handler,
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
            AdminDeviceOnboardingRequestMapper.Map(request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Artifact is null || result.ActivationPayload is null)
        {
            return result.ErrorCode switch
            {
                AdminCreateDeviceOnboardingArtifactErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminCreateDeviceOnboardingArtifactErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Device onboarding artifact cannot be created.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device onboarding creation request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Created(
            $"/api/v1/admin/tenants/{result.Artifact.TenantId:D}/device-onboarding-artifacts/{result.Artifact.ActivationCodeId:D}",
            AdminDeviceOnboardingRequestMapper.MapCreateResponse(result.Artifact, result.ActivationPayload));
    }

    private static async Task<IResult> RevokeAsync(
        Guid tenantId,
        Guid activationCodeId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminRevokeDeviceOnboardingArtifactHandler handler,
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
            new AdminDeviceOnboardingRouteRequest
            {
                TenantId = tenantId,
                ActivationCodeId = activationCodeId,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Artifact is null)
        {
            return result.ErrorCode switch
            {
                AdminRevokeDeviceOnboardingArtifactErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminRevokeDeviceOnboardingArtifactErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Device onboarding artifact was not found.",
                    result.ErrorMessage),
                AdminRevokeDeviceOnboardingArtifactErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Device onboarding artifact cannot be revoked.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device onboarding revoke request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminDeviceOnboardingRequestMapper.MapResponse(result.Artifact));
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
