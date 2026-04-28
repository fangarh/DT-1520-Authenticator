using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminRuntimeConfigurationApiTests
{
    [Fact]
    public async Task GetRuntimeConfiguration_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/admin/runtime-configuration");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRuntimeConfiguration_ReturnsCallbackPolicy_ForAuthenticatedOperator()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync("/api/v1/admin/runtime-configuration");
        var body = await response.Content.ReadFromJsonAsync<AdminRuntimeConfigurationHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("PublicInternet", body!.CallbackUrlPolicy.Mode);
        Assert.False(body.CallbackUrlPolicy.AllowInsecureHttp);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username,
                password,
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/admin/auth/csrf-token");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AdminCsrfTokenHttpResponse>();
        Assert.NotNull(body);
        return body!.RequestToken;
    }
}
