using OtpAuth.Api.Authentication;
using OtpAuth.Api.Devices;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Endpoints;

public static class DevicesEndpoints
{
    public static IEndpointRouteBuilder MapDevicesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/devices/me/challenges/pending", ListPendingChallengesAsync)
            .RequireAuthorization("DeviceChallengeRead")
            .WithName("ListPendingDeviceChallenges");

        app.MapGet("/api/v1/devices", ListDevicesAsync)
            .RequireAuthorization("DevicesWrite")
            .WithName("ListDevices");

        app.MapPost("/api/v1/devices/activate", ActivateDeviceAsync)
            .RequireAuthorization("DevicesWrite")
            .WithName("ActivateDevice");

        app.MapPost("/api/v1/devices/{deviceId:guid}/revoke", RevokeDeviceAsync)
            .RequireAuthorization("DevicesWrite")
            .WithName("RevokeDevice");

        return app;
    }

    private static async Task<IResult> ListPendingChallengesAsync(
        ListPendingPushChallengesForDeviceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        DeviceClientContext deviceContext;
        try
        {
            deviceContext = httpContext.GetRequiredDeviceClientContext();
        }
        catch (InvalidOperationException)
        {
            return CreateProblem(StatusCodes.Status401Unauthorized, "Authentication failed.", "Authenticated principal is missing device claims.");
        }

        var result = await handler.HandleAsync(deviceContext, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                ListPendingPushChallengesForDeviceErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device challenge request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(result.Challenges.Select(DeviceChallengeResponseMapper.MapPendingChallenge));
    }

    private static async Task<IResult> ListDevicesAsync(
        string externalUserId,
        bool? pushCapableOnly,
        ListDevicesForRoutingHandler handler,
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
            externalUserId,
            pushCapableOnly ?? false,
            clientContext,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                ListDevicesForRoutingErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device list request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(result.Devices.Select(DeviceRequestMapper.MapDeviceResponse));
    }

    private static async Task<IResult> ActivateDeviceAsync(
        ActivateDeviceHttpRequest request,
        ActivateDeviceHandler handler,
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

        if (!DeviceRequestMapper.TryMap(request, out var applicationRequest, out var validationError))
        {
            return CreateProblem(StatusCodes.Status400BadRequest, "Invalid device activation request.", validationError);
        }

        var result = await handler.HandleAsync(applicationRequest!, clientContext, cancellationToken);
        if (!result.IsSuccess || result.Device is null || result.Tokens is null)
        {
            return result.ErrorCode switch
            {
                ActivateDeviceErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                ActivateDeviceErrorCode.InvalidActivationCode => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Device activation failed.",
                    result.ErrorMessage),
                ActivateDeviceErrorCode.Conflict => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Device activation could not be completed.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device activation request.",
                    result.ErrorMessage),
            };
        }

        var location = $"/api/v1/devices/{result.Device.DeviceId}";
        httpContext.Response.Headers.Location = location;

        return Results.Created(
            location,
            DeviceRequestMapper.MapActivationResponse(result.Device, result.Tokens));
    }

    private static async Task<IResult> RevokeDeviceAsync(
        Guid deviceId,
        RevokeDeviceHandler handler,
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

        var result = await handler.HandleAsync(deviceId, clientContext, cancellationToken);
        if (!result.IsSuccess || result.Device is null)
        {
            return result.ErrorCode switch
            {
                RevokeDeviceErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                RevokeDeviceErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Device was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid device revoke request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(DeviceRequestMapper.MapDeviceResponse(result.Device));
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
