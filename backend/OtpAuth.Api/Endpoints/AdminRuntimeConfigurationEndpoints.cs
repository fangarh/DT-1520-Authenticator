using OtpAuth.Api.Admin;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Api.Endpoints;

public static class AdminRuntimeConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapAdminRuntimeConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/runtime-configuration", GetAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.AuthenticatedPolicy)
            .WithName("AdminGetRuntimeConfiguration");

        return app;
    }

    private static IResult GetAsync(
        ChallengeCallbackUrlPolicy callbackUrlPolicy,
        HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        return Results.Ok(new AdminRuntimeConfigurationHttpResponse
        {
            CallbackUrlPolicy = new AdminCallbackUrlPolicyHttpResponse
            {
                Mode = callbackUrlPolicy.ModeName,
                AllowInsecureHttp = callbackUrlPolicy.AllowInsecureHttp,
            },
        });
    }
}
