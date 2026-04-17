using OtpAuth.Api.Auth;
using OtpAuth.Api.Devices;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/oauth2/token", IssueIntegrationTokenAsync)
            .AllowAnonymous()
            .WithName("IssueIntegrationToken");
        app.MapPost("/oauth2/introspect", IntrospectIntegrationTokenAsync)
            .AllowAnonymous()
            .WithName("IntrospectIntegrationToken");
        app.MapPost("/oauth2/revoke", RevokeIntegrationTokenAsync)
            .AllowAnonymous()
            .WithName("RevokeIntegrationToken");
        app.MapPost("/api/v1/auth/device-tokens/refresh", RefreshDeviceTokenAsync)
            .AllowAnonymous()
            .WithName("RefreshDeviceToken");

        return app;
    }

    private static async Task<IResult> IssueIntegrationTokenAsync(
        HttpRequest request,
        IssueIntegrationTokenHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return CreateProblem(StatusCodes.Status400BadRequest, "Invalid token request.", "Content type must be application/x-www-form-urlencoded.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var applicationRequest = IssueIntegrationTokenRequestMapper.Map(new IssueIntegrationTokenFormRequest
        {
            GrantType = form["grant_type"].ToString(),
            ClientId = form["client_id"].ToString(),
            ClientSecret = form["client_secret"].ToString(),
            Scope = form["scope"].ToString(),
        });

        var result = await handler.HandleAsync(applicationRequest, cancellationToken);
        if (!result.IsSuccess || result.Token is null)
        {
            return result.ErrorCode switch
            {
                IssueIntegrationTokenErrorCode.InvalidClient => CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Client authentication failed.",
                    result.ErrorMessage),
                IssueIntegrationTokenErrorCode.InvalidScope => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid token scope.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid token request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(IssueIntegrationTokenRequestMapper.MapResponse(result.Token));
    }

    private static async Task<IResult> IntrospectIntegrationTokenAsync(
        HttpRequest request,
        IntrospectIntegrationTokenHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return CreateProblem(StatusCodes.Status400BadRequest, "Invalid introspection request.", "Content type must be application/x-www-form-urlencoded.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var applicationRequest = IntrospectIntegrationTokenRequestMapper.Map(new IntrospectIntegrationTokenFormRequest
        {
            ClientId = form["client_id"].ToString(),
            ClientSecret = form["client_secret"].ToString(),
            Token = form["token"].ToString(),
            TokenTypeHint = form["token_type_hint"].ToString(),
        });

        var result = await handler.HandleAsync(applicationRequest, cancellationToken);
        if (!result.IsSuccess || result.Introspection is null)
        {
            return result.ErrorCode switch
            {
                IntrospectIntegrationTokenErrorCode.InvalidClient => CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Client authentication failed.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid introspection request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(IntrospectIntegrationTokenRequestMapper.MapResponse(result.Introspection));
    }

    private static async Task<IResult> RevokeIntegrationTokenAsync(
        HttpRequest request,
        RevokeIntegrationTokenHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return CreateProblem(StatusCodes.Status400BadRequest, "Invalid revocation request.", "Content type must be application/x-www-form-urlencoded.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var applicationRequest = RevokeIntegrationTokenRequestMapper.Map(new RevokeIntegrationTokenFormRequest
        {
            ClientId = form["client_id"].ToString(),
            ClientSecret = form["client_secret"].ToString(),
            Token = form["token"].ToString(),
            TokenTypeHint = form["token_type_hint"].ToString(),
        });

        var result = await handler.HandleAsync(applicationRequest, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                RevokeIntegrationTokenErrorCode.InvalidClient => CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Client authentication failed.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid revocation request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok();
    }

    private static async Task<IResult> RefreshDeviceTokenAsync(
        RefreshDeviceTokenHttpRequest request,
        RefreshDeviceTokenHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(DeviceRequestMapper.Map(request), cancellationToken);
        if (!result.IsSuccess || result.Tokens is null)
        {
            return result.ErrorCode switch
            {
                RefreshDeviceTokenErrorCode.InvalidToken => CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Device token refresh failed.",
                    result.ErrorMessage),
                RefreshDeviceTokenErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Device token refresh failed.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device token refresh request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(DeviceRequestMapper.MapTokenResponse(result.Tokens));
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
