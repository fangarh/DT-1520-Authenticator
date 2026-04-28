using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminIntegrationClientEndpoints
{
    public static IEndpointRouteBuilder MapAdminIntegrationClientEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/admin/tenants/{tenantId:guid}/integration-clients",
                ListAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.IntegrationClientsReadPolicy)
            .WithName("AdminListIntegrationClients");

        app.MapPost("/api/v1/admin/integration-clients", CreateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.IntegrationClientsWritePolicy)
            .WithName("AdminCreateIntegrationClient");

        app.MapPost(
                "/api/v1/admin/tenants/{tenantId:guid}/integration-clients/{clientId}/rotate-secret",
                RotateSecretAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.IntegrationClientsWritePolicy)
            .WithName("AdminRotateIntegrationClientSecret");

        app.MapPut(
                "/api/v1/admin/tenants/{tenantId:guid}/integration-clients/{clientId}/scopes",
                UpdateScopesAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.IntegrationClientsWritePolicy)
            .WithName("AdminUpdateIntegrationClientScopes");

        app.MapPost(
                "/api/v1/admin/tenants/{tenantId:guid}/integration-clients/{clientId}/deactivate",
                DeactivateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.IntegrationClientsWritePolicy)
            .WithName("AdminDeactivateIntegrationClient");

        app.MapPost(
                "/api/v1/admin/tenants/{tenantId:guid}/integration-clients/{clientId}/reactivate",
                ReactivateAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.IntegrationClientsWritePolicy)
            .WithName("AdminReactivateIntegrationClient");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        HttpContext httpContext,
        AdminListIntegrationClientsHandler handler,
        CancellationToken cancellationToken)
    {
        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(
            new AdminIntegrationClientListRequest
            {
                TenantId = tenantId,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                AdminListIntegrationClientsErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminListIntegrationClientsErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Integration clients were not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid integration client lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(result.Clients.Select(AdminIntegrationClientRequestMapper.MapResponse));
    }

    private static async Task<IResult> CreateAsync(
        AdminCreateIntegrationClientHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminCreateIntegrationClientHandler handler,
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
            AdminIntegrationClientRequestMapper.Map(request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Client is null || result.ClientSecret is null)
        {
            return result.ErrorCode switch
            {
                AdminCreateIntegrationClientErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminCreateIntegrationClientErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Integration client cannot be created.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid integration client creation request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Created(
            $"/api/v1/admin/tenants/{result.Client.TenantId:D}/integration-clients",
            AdminIntegrationClientRequestMapper.MapCreateResponse(result.Client, result.ClientSecret));
    }

    private static async Task<IResult> RotateSecretAsync(
        Guid tenantId,
        string clientId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminRotateIntegrationClientSecretHandler handler,
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
            new AdminIntegrationClientRouteRequest
            {
                TenantId = tenantId,
                ClientId = clientId,
            },
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Client is null || result.ClientSecret is null)
        {
            return result.ErrorCode switch
            {
                AdminRotateIntegrationClientSecretErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminRotateIntegrationClientSecretErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Integration client was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid integration client secret rotation request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminIntegrationClientRequestMapper.MapRotateSecretResponse(result.Client, result.ClientSecret));
    }

    private static async Task<IResult> UpdateScopesAsync(
        Guid tenantId,
        string clientId,
        AdminUpdateIntegrationClientScopesHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminUpdateIntegrationClientScopesHandler handler,
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
            AdminIntegrationClientRequestMapper.Map(tenantId, clientId, request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Client is null)
        {
            return result.ErrorCode switch
            {
                AdminUpdateIntegrationClientScopesErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminUpdateIntegrationClientScopesErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Integration client was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid integration client scope update request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminIntegrationClientRequestMapper.MapResponse(result.Client));
    }

    private static Task<IResult> DeactivateAsync(
        Guid tenantId,
        string clientId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminSetIntegrationClientActiveStateHandler handler,
        CancellationToken cancellationToken)
    {
        return SetActiveStateAsync(tenantId, clientId, isActive: false, httpContext, antiforgery, handler, cancellationToken);
    }

    private static Task<IResult> ReactivateAsync(
        Guid tenantId,
        string clientId,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminSetIntegrationClientActiveStateHandler handler,
        CancellationToken cancellationToken)
    {
        return SetActiveStateAsync(tenantId, clientId, isActive: true, httpContext, antiforgery, handler, cancellationToken);
    }

    private static async Task<IResult> SetActiveStateAsync(
        Guid tenantId,
        string clientId,
        bool isActive,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminSetIntegrationClientActiveStateHandler handler,
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
            new AdminIntegrationClientRouteRequest
            {
                TenantId = tenantId,
                ClientId = clientId,
            },
            isActive,
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Client is null)
        {
            return result.ErrorCode switch
            {
                AdminSetIntegrationClientActiveStateErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminSetIntegrationClientActiveStateErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Integration client was not found.",
                    result.ErrorMessage),
                AdminSetIntegrationClientActiveStateErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Integration client state cannot be changed.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid integration client state update request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminIntegrationClientRequestMapper.MapResponse(result.Client));
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
