using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeviceOnboardingApiTests
{
    [Fact]
    public async Task ListArtifacts_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync(ListPath());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateArtifact_ReturnsCreatedPayloadOnlyOnce_AndWritesAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.DevicesRead, AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/device-onboarding-artifacts", CreateRequest());
        var body = await response.Content.ReadFromJsonAsync<AdminCreateDeviceOnboardingArtifactHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending", body!.Artifact.Status);
        Assert.Equal("android", body.Artifact.Platform);
        Assert.StartsWith("dac_", body.ActivationPayload, StringComparison.Ordinal);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("codeHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("activation-secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            factory.GetAdminDeviceOnboardingAuditWriter().Events,
            item => item.EventType == "created" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.ActivationCodeId == body.Artifact.ActivationCodeId);

        var listResponse = await client.GetAsync(ListPath());
        var listBody = await listResponse.Content.ReadFromJsonAsync<List<AdminDeviceOnboardingArtifactHttpResponse>>();
        var listRaw = await listResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listBody);
        Assert.Contains(listBody!, item => item.ActivationCodeId == body.Artifact.ActivationCodeId);
        Assert.DoesNotContain(body.ActivationPayload, listRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateArtifact_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.PostAsJsonAsync("/api/v1/admin/device-onboarding-artifacts", CreateRequest());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid anti-forgery token.", problem!.Title);
    }

    [Fact]
    public async Task CreateArtifact_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/device-onboarding-artifacts", CreateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokeArtifact_ReturnsRevoked_AndPreventsSecondRevoke()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var createResponse = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/device-onboarding-artifacts", CreateRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<AdminCreateDeviceOnboardingArtifactHttpResponse>();

        var revokeResponse = await PostWithCsrfAsync(client, csrfToken, RevokePath(created!.Artifact.ActivationCodeId), new { });
        var revoked = await revokeResponse.Content.ReadFromJsonAsync<AdminDeviceOnboardingArtifactHttpResponse>();
        var secondRevokeResponse = await PostWithCsrfAsync(client, csrfToken, RevokePath(created.Artifact.ActivationCodeId), new { });

        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.NotNull(revoked);
        Assert.Equal("revoked", revoked!.Status);
        Assert.Equal(HttpStatusCode.Conflict, secondRevokeResponse.StatusCode);
        Assert.Contains(
            factory.GetAdminDeviceOnboardingAuditWriter().Events,
            item => item.EventType == "revoked" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.ActivationCodeId == created.Artifact.ActivationCodeId);
    }

    [Fact]
    public async Task ListArtifacts_ReturnsBadRequest_WhenStatusIsInvalid()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync($"{ListPath()}?status=unknown");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid device onboarding lookup request.", problem!.Title);
    }

    private static string ListPath()
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/device-onboarding-artifacts";
    }

    private static string RevokePath(Guid activationCodeId)
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/device-onboarding-artifacts/{activationCodeId}/revoke";
    }

    private static object CreateRequest()
    {
        return new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            externalUserId = "user-qr",
            platform = "android",
            ttlMinutes = 10,
        };
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

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client,
        string csrfToken,
        string uri,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }
}
