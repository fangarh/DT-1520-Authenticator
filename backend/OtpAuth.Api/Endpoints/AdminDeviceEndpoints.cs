using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminDeviceEndpoints
{
    public static IEndpointRouteBuilder MapAdminDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/admin/tenants/{tenantId:guid}/users/{externalUserId}/devices",
                ListAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.DevicesReadPolicy)
            .WithName("AdminListUserDevices");

        app.MapPost(
                "/api/v1/admin/tenants/{tenantId:guid}/users/{externalUserId}/devices/{deviceId:guid}/revoke",
                RevokeAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.DevicesWritePolicy)
            .WithName("AdminRevokeUserDevice");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        string externalUserId,
        HttpContext httpContext,
        AdminListUserDevicesHandler handler,
        CancellationToken cancellationToken)
    {
        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(
            new AdminUserDeviceListRequest
            {
                TenantId = tenantId,
                ExternalUserId = externalUserId,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                AdminListUserDevicesErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminListUserDevicesErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Devices were not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(result.Devices.Select(AdminDeviceRequestMapper.MapResponse));
    }

    private static async Task<IResult> RevokeAsync(
        Guid tenantId,
        string externalUserId,
        Guid deviceId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminRevokeUserDeviceHandler handler,
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
            new AdminRevokeUserDeviceRequest
            {
                TenantId = tenantId,
                ExternalUserId = externalUserId,
                DeviceId = deviceId,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Device is null)
        {
            return result.ErrorCode switch
            {
                AdminRevokeUserDeviceErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminRevokeUserDeviceErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Device was not found.",
                    result.ErrorMessage),
                AdminRevokeUserDeviceErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Device cannot be revoked.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device revoke request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(AdminDeviceRequestMapper.MapResponse(result.Device));
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
