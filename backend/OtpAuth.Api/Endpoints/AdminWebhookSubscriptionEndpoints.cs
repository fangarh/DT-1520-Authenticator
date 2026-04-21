using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminWebhookSubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapAdminWebhookSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/admin/tenants/{tenantId:guid}/webhook-subscriptions",
                ListAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.WebhooksReadPolicy)
            .WithName("AdminListWebhookSubscriptions");

        app.MapPost("/api/v1/admin/webhook-subscriptions", UpsertAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.WebhooksWritePolicy)
            .WithName("AdminUpsertWebhookSubscription");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        Guid? applicationClientId,
        HttpContext httpContext,
        AdminListWebhookSubscriptionsHandler handler,
        CancellationToken cancellationToken)
    {
        var adminContext = GetAdminContextOrProblem(httpContext, out var authError);
        if (authError is not null)
        {
            return authError;
        }

        var result = await handler.HandleAsync(
            tenantId,
            applicationClientId,
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                AdminListWebhookSubscriptionsErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminListWebhookSubscriptionsErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Application client was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid webhook subscription lookup request.",
                    result.ErrorMessage),
            };
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(result.Subscriptions.Select(AdminWebhookSubscriptionRequestMapper.MapResponse));
    }

    private static async Task<IResult> UpsertAsync(
        AdminUpsertWebhookSubscriptionHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminUpsertWebhookSubscriptionHandler handler,
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
            AdminWebhookSubscriptionRequestMapper.Map(request),
            adminContext!,
            cancellationToken);
        if (!result.IsSuccess || result.Subscription is null)
        {
            return result.ErrorCode switch
            {
                AdminUpsertWebhookSubscriptionErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                AdminUpsertWebhookSubscriptionErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Application client was not found.",
                    result.ErrorMessage),
                AdminUpsertWebhookSubscriptionErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Webhook subscription cannot be saved.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid webhook subscription request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(AdminWebhookSubscriptionRequestMapper.MapResponse(result.Subscription));
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
