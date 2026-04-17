using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Endpoints;

public static class AdminAuthEndpoints
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/auth/csrf-token", IssueCsrfTokenAsync)
            .AllowAnonymous()
            .WithName("IssueAdminCsrfToken");

        app.MapPost("/api/v1/admin/auth/login", LoginAsync)
            .AllowAnonymous()
            .WithName("AdminLogin");

        app.MapPost("/api/v1/admin/auth/logout", LogoutAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.AuthenticatedPolicy)
            .WithName("AdminLogout");

        app.MapGet("/api/v1/admin/auth/session", GetSessionAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.AuthenticatedPolicy)
            .WithName("GetAdminSession");

        return app;
    }

    private static IResult IssueCsrfTokenAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(new AdminCsrfTokenHttpResponse
        {
            RequestToken = tokens.RequestToken ?? string.Empty,
        });
    }

    private static async Task<IResult> LoginAsync(
        AdminLoginHttpRequest request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        AdminLoginHandler handler,
        CancellationToken cancellationToken)
    {
        var csrfError = await ValidateAntiforgeryAsync(httpContext, antiforgery);
        if (csrfError is not null)
        {
            return csrfError;
        }

        var result = await handler.HandleAsync(
            AdminAuthRequestMapper.Map(request, GetRemoteAddress(httpContext)),
            cancellationToken);
        if (!result.IsSuccess || result.User is null)
        {
            return result.ErrorCode switch
            {
                AdminLoginErrorCode.ValidationFailed => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid admin login request.",
                    result.ErrorMessage),
                AdminLoginErrorCode.RateLimited => CreateRateLimitedProblem(httpContext, result),
                _ => CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Admin authentication failed.",
                    result.ErrorMessage),
            };
        }

        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User.AdminUserId.ToString()),
            new(ClaimTypes.Name, result.User.Username),
        };
        claims.AddRange(result.User.Permissions.Select(static permission => new Claim(AdminClaimTypes.Permission, permission)));

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, AdminAuthenticationDefaults.AuthenticationScheme));
        await httpContext.SignInAsync(
            AdminAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false,
                IssuedUtc = now,
                ExpiresUtc = now + SessionLifetime,
            });

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(AdminAuthRequestMapper.MapResponse(result.User));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        IAdminAuthAuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        var csrfError = await ValidateAntiforgeryAsync(httpContext, antiforgery);
        if (csrfError is not null)
        {
            return csrfError;
        }

        AdminContext adminContext;
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

        await httpContext.SignOutAsync(AdminAuthenticationDefaults.AuthenticationScheme);
        await auditWriter.WriteLogoutAsync(
            new AdminAuthenticatedUser
            {
                AdminUserId = adminContext.AdminUserId,
                Username = adminContext.Username,
                Permissions = adminContext.Permissions,
            },
            GetRemoteAddress(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static IResult GetSessionAsync(HttpContext httpContext)
    {
        AdminContext adminContext;
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

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(new AdminSessionHttpResponse
        {
            AdminUserId = adminContext.AdminUserId,
            Username = adminContext.Username,
            Permissions = adminContext.Permissions,
        });
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

    private static IResult CreateRateLimitedProblem(HttpContext httpContext, AdminLoginResult result)
    {
        if (result.RetryAfterSeconds is int retryAfterSeconds && retryAfterSeconds > 0)
        {
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }

        return CreateProblem(
            StatusCodes.Status429TooManyRequests,
            "Admin login rate limited.",
            result.ErrorMessage,
            result.RetryAfterSeconds);
    }

    private static IResult CreateProblem(int statusCode, string title, string? detail, int? retryAfterSeconds = null)
    {
        var extensions = retryAfterSeconds is null
            ? null
            : new Dictionary<string, object?>
            {
                ["retryAfterSeconds"] = retryAfterSeconds,
            };

        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            type: $"https://otpauth.dev/problems/{statusCode}",
            extensions: extensions);
    }

    private static string? GetRemoteAddress(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstForwardedIp = forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstForwardedIp))
            {
                return firstForwardedIp;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
