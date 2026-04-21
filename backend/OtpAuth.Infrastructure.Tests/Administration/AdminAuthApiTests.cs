using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminAuthApiTests
{
    [Fact]
    public async Task GetSession_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/admin/auth/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new
        {
            username = "operator",
            password = "super-secret",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsSession_AndPersistsCookie_WhenCredentialsAreValid()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsWrite]);
        var auditWriter = factory.GetAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostLoginAsync(client, csrfToken, "operator", "super-secret");
        var body = await response.Content.ReadFromJsonAsync<AdminSessionHttpResponse>();
        var sessionResponse = await client.GetAsync("/api/v1/admin/auth/session");
        var sessionBody = await sessionResponse.Content.ReadFromJsonAsync<AdminSessionHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("operator", body!.Username);
        Assert.Contains(AdminPermissions.EnrollmentsWrite, body.Permissions);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("otpauth-admin-session=", StringComparison.Ordinal));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.NotNull(sessionBody);
        Assert.Equal(body.AdminUserId, sessionBody!.AdminUserId);
        Assert.Single(auditWriter.LoginSucceededUsernames);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        var auditWriter = factory.GetAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostLoginAsync(client, csrfToken, "operator", "wrong-secret");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(auditWriter.LoginFailures);
        Assert.False(auditWriter.LoginFailures[0].IsRateLimited);
    }

    [Fact]
    public async Task Login_ReturnsTooManyRequests_AfterRepeatedFailures()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        var auditWriter = factory.GetAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            response = await PostLoginAsync(client, csrfToken, "operator", "wrong-secret");
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        Assert.NotEmpty(retryAfterValues);
        Assert.Contains(auditWriter.LoginFailures, failure => failure.IsRateLimited);
    }

    [Fact]
    public async Task Logout_ClearsSession_AndWritesAuditEvent()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsWrite]);
        var auditWriter = factory.GetAuditWriter();
        using var client = factory.CreateAdminClient();

        var loginCsrfToken = await GetCsrfTokenAsync(client);
        var loginResponse = await PostLoginAsync(client, loginCsrfToken, "operator", "super-secret");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var logoutCsrfToken = await GetCsrfTokenAsync(client);
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/logout");
        logoutRequest.Headers.Add("X-CSRF-TOKEN", logoutCsrfToken);
        var logoutResponse = await client.SendAsync(logoutRequest);
        var sessionResponse = await client.GetAsync("/api/v1/admin/auth/session");

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Contains(logoutResponse.Headers.GetValues("Set-Cookie"), value => value.Contains("otpauth-admin-session=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
        Assert.Single(auditWriter.LogoutUsernames);
    }

    [Fact]
    public async Task ProductionCookies_AreMarkedSecure()
    {
        await using var factory = new AdminAuthApiTestFactory("Production");
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient(useHttps: true);

        var csrfResponse = await client.GetAsync("/api/v1/admin/auth/csrf-token");
        var csrfBody = await csrfResponse.Content.ReadFromJsonAsync<AdminCsrfTokenHttpResponse>();
        var loginResponse = await PostLoginAsync(client, csrfBody!.RequestToken, "operator", "super-secret");

        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        Assert.Contains(csrfResponse.Headers.GetValues("Set-Cookie"), value => value.Contains("secure", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(loginResponse.Headers.GetValues("Set-Cookie"), value => value.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ForwardedHeadersMiddleware_PromotesRequestToHttps_WhenProxyIsTrusted()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 2,
            RequireHeaderSymmetry = false,
        };
        options.KnownProxies.Add(IPAddress.Loopback);

        string? observedScheme = null;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                observedScheme = context.Request.Scheme;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-Proto"] = "https";
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        httpContext.Request.Scheme = Uri.UriSchemeHttp;

        await middleware.Invoke(httpContext);

        Assert.Equal(Uri.UriSchemeHttps, observedScheme);
        Assert.True(httpContext.Request.IsHttps);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/admin/auth/csrf-token");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AdminCsrfTokenHttpResponse>();
        Assert.NotNull(body);
        return body!.RequestToken;
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string csrfToken, string username, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username,
                password,
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return client.SendAsync(request);
    }
}
