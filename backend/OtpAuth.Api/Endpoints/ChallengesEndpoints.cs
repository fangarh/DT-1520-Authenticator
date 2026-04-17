using OtpAuth.Api.Authentication;
using OtpAuth.Api.Challenges;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Endpoints;

public static class ChallengesEndpoints
{
    public static IEndpointRouteBuilder MapChallengesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/challenges/{challengeId:guid}", GetChallengeAsync)
            .RequireAuthorization("ChallengesRead")
            .WithName("GetChallenge");

        app.MapPost("/api/v1/challenges/{challengeId:guid}/verify-totp", VerifyTotpAsync)
            .RequireAuthorization("ChallengesWrite")
            .WithName("VerifyTotp");

        app.MapPost("/api/v1/challenges/{challengeId:guid}/verify-backup-code", VerifyBackupCodeAsync)
            .RequireAuthorization("ChallengesWrite")
            .WithName("VerifyBackupCode");

        app.MapPost("/api/v1/challenges", CreateChallengeAsync)
            .RequireAuthorization("ChallengesWrite")
            .WithName("CreateChallenge");

        return app;
    }

    private static async Task<IResult> GetChallengeAsync(
        Guid challengeId,
        GetChallengeHandler handler,
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

        var result = await handler.HandleAsync(challengeId, clientContext, cancellationToken);
        if (!result.IsSuccess || result.Challenge is null)
        {
            return result.ErrorCode switch
            {
                GetChallengeErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                GetChallengeErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Challenge was not found.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid challenge identifier.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(CreateChallengeRequestMapper.MapResponse(result.Challenge));
    }

    private static async Task<IResult> CreateChallengeAsync(
        CreateChallengeHttpRequest request,
        CreateChallengeHandler handler,
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

        if (!CreateChallengeRequestMapper.TryMap(request, out var applicationRequest, out var validationError))
        {
            return CreateProblem(StatusCodes.Status400BadRequest, "Invalid challenge request.", validationError);
        }

        var result = await handler.HandleAsync(applicationRequest!, clientContext, cancellationToken);
        if (!result.IsSuccess || result.Challenge is null)
        {
            return result.ErrorCode switch
            {
                CreateChallengeErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                CreateChallengeErrorCode.PolicyDenied => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Challenge creation denied by policy.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid challenge request.",
                    result.ErrorMessage),
            };
        }

        var response = CreateChallengeRequestMapper.MapResponse(result.Challenge);
        var location = $"/api/v1/challenges/{result.Challenge.Id}";
        httpContext.Response.Headers.Location = location;

        return Results.Created(location, response);
    }

    private static async Task<IResult> VerifyTotpAsync(
        Guid challengeId,
        VerifyTotpHttpRequest request,
        VerifyTotpHandler handler,
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
            VerifyTotpRequestMapper.Map(challengeId, request),
            clientContext,
            cancellationToken);

        if (!result.IsSuccess || result.Challenge is null)
        {
            return result.ErrorCode switch
            {
                VerifyTotpErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                VerifyTotpErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Challenge was not found.",
                    result.ErrorMessage),
                VerifyTotpErrorCode.InvalidCode => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Challenge verification failed.",
                    result.ErrorMessage),
                VerifyTotpErrorCode.RateLimited => CreateRateLimitedProblem(httpContext, result),
                VerifyTotpErrorCode.ChallengeExpired => CreateProblem(
                    StatusCodes.Status410Gone,
                    "Challenge has expired.",
                    result.ErrorMessage),
                VerifyTotpErrorCode.InvalidState => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Challenge is not in a verifiable state.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid TOTP verification request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(CreateChallengeRequestMapper.MapResponse(result.Challenge));
    }

    private static async Task<IResult> VerifyBackupCodeAsync(
        Guid challengeId,
        VerifyBackupCodeHttpRequest request,
        VerifyBackupCodeHandler handler,
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
            VerifyBackupCodeRequestMapper.Map(challengeId, request),
            clientContext,
            cancellationToken);

        if (!result.IsSuccess || result.Challenge is null)
        {
            return result.ErrorCode switch
            {
                VerifyBackupCodeErrorCode.AccessDenied => CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Access denied.",
                    result.ErrorMessage),
                VerifyBackupCodeErrorCode.NotFound => CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Challenge was not found.",
                    result.ErrorMessage),
                VerifyBackupCodeErrorCode.InvalidCode => CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Challenge verification failed.",
                    result.ErrorMessage),
                VerifyBackupCodeErrorCode.RateLimited => CreateRateLimitedProblem(httpContext, result),
                VerifyBackupCodeErrorCode.ChallengeExpired => CreateProblem(
                    StatusCodes.Status410Gone,
                    "Challenge has expired.",
                    result.ErrorMessage),
                VerifyBackupCodeErrorCode.InvalidState => CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Challenge is not in a verifiable state.",
                    result.ErrorMessage),
                _ => CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid backup code verification request.",
                    result.ErrorMessage),
            };
        }

        return Results.Ok(CreateChallengeRequestMapper.MapResponse(result.Challenge));
    }

    private static IResult CreateProblem(int statusCode, string title, string? detail, int? retryAfterSeconds = null)
    {
        var extensions = retryAfterSeconds.HasValue
            ? new Dictionary<string, object?>
            {
                ["retryAfterSeconds"] = retryAfterSeconds.Value,
            }
            : null;

        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            type: $"https://otpauth.dev/problems/{statusCode}",
            extensions: extensions);
    }

    private static IResult CreateRateLimitedProblem(HttpContext httpContext, VerifyTotpResult result)
    {
        if (result.RetryAfterSeconds.HasValue)
        {
            httpContext.Response.Headers["Retry-After"] = result.RetryAfterSeconds.Value.ToString();
        }

        return CreateProblem(
            StatusCodes.Status429TooManyRequests,
            "Too many verification attempts.",
            result.ErrorMessage,
            result.RetryAfterSeconds);
    }

    private static IResult CreateRateLimitedProblem(HttpContext httpContext, VerifyBackupCodeResult result)
    {
        if (result.RetryAfterSeconds.HasValue)
        {
            httpContext.Response.Headers["Retry-After"] = result.RetryAfterSeconds.Value.ToString();
        }

        return CreateProblem(
            StatusCodes.Status429TooManyRequests,
            "Too many verification attempts.",
            result.ErrorMessage,
            result.RetryAfterSeconds);
    }
}
