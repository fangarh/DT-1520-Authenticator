using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminTenantDirectoryEndpoints
{
    public static IEndpointRouteBuilder MapAdminTenantDirectoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/tenants", ListAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.TenantsReadPolicy)
            .WithName("AdminListTenants");

        app.MapGet("/api/v1/admin/tenants/{tenantId:guid}/directory", GetDirectoryAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.TenantsReadPolicy)
            .WithName("AdminGetTenantDirectory");

        app.MapPost("/api/v1/admin/tenants", CreateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.TenantsWritePolicy)
            .WithName("AdminCreateTenant");

        app.MapPost("/api/v1/admin/tenants/quick-create", QuickCreateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.TenantsWritePolicy)
            .WithName("AdminQuickCreateTenant");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        AdminListTenantDirectoryHandler handler,
        CancellationToken cancellationToken)
    {
        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(new AdminTenantDirectoryListRequest(), adminContext!, cancellationToken);
        if (!result.IsSuccess)
        {
            return CreateProblem(StatusCodes.Status403Forbidden, "Access denied.", result.ErrorMessage);
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(result.Tenants.Select(AdminTenantDirectoryRequestMapper.MapTenant));
    }

    private static async Task<IResult> GetDirectoryAsync(
        Guid tenantId,
        HttpContext httpContext,
        AdminGetTenantDirectoryHandler handler,
        CancellationToken cancellationToken)
    {
        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(
            new AdminTenantDirectoryDetailRequest
            {
                TenantId = tenantId,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Directory is null)
        {
            return result.ErrorCode switch
            {
                AdminGetTenantDirectoryErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminGetTenantDirectoryErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Tenant directory was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid tenant directory lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminTenantDirectoryRequestMapper.MapDirectory(result.Directory));
    }

    private static async Task<IResult> CreateAsync(
        AdminCreateTenantHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminCreateTenantHandler handler,
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

        AdminTenantCreateRequest applicationRequest;
        try
        {
            applicationRequest = AdminTenantDirectoryRequestMapper.Map(request);
        }
        catch (InvalidOperationException exception)
        {
            return CreateProblem(StatusCodes.Status400BadRequest, "Invalid tenant creation request.", exception.Message);
        }

        var result = await handler.HandleAsync(applicationRequest, adminContext!, cancellationToken);
        if (!result.IsSuccess || result.Tenant is null)
        {
            return result.ErrorCode switch
            {
                AdminCreateTenantErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminCreateTenantErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Tenant cannot be created.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid tenant creation request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Created(
            $"/api/v1/admin/tenants/{result.Tenant.TenantId:D}/directory",
            AdminTenantDirectoryRequestMapper.MapTenant(result.Tenant));
    }

    private static async Task<IResult> QuickCreateAsync(
        AdminQuickCreateTenantHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminQuickCreateTenantHandler handler,
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
            AdminTenantDirectoryRequestMapper.Map(request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Directory is null || result.Client is null || result.ClientSecret is null)
        {
            return result.ErrorCode switch
            {
                AdminQuickCreateTenantErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminQuickCreateTenantErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Tenant cannot be quick-created.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid tenant quick-create request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Created(
            $"/api/v1/admin/tenants/{result.Directory.Tenant.TenantId:D}/directory",
            AdminTenantDirectoryRequestMapper.MapQuickCreate(result.Directory, result.Client, result.ClientSecret));
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
