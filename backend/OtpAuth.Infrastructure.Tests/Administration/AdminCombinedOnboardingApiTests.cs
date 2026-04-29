using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminCombinedOnboardingApiTests
{
    [Fact]
    public async Task CreatePackage_ReturnsOneTimeDeviceAndTotpPayloads_AndReadModelsStaySanitized()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [
                AdminPermissions.DevicesRead,
                AdminPermissions.DevicesWrite,
                AdminPermissions.EnrollmentsRead,
                AdminPermissions.EnrollmentsWrite,
            ]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(
            client,
            csrfToken,
            "/api/v1/admin/combined-onboarding-packages",
            CreateRequest());
        var body = await response.Content.ReadFromJsonAsync<AdminCreateCombinedOnboardingPackageHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending", body!.DeviceArtifact.Status);
        Assert.Equal("android", body.DeviceArtifact.Platform);
        Assert.Equal("pending", body.TotpEnrollment.Status);
        Assert.StartsWith("dac_", body.ActivationPayload, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(body.TotpEnrollment.SecretUri));
        Assert.False(string.IsNullOrWhiteSpace(body.TotpEnrollment.QrCodePayload));
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("codeHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            factory.GetAdminDeviceOnboardingAuditWriter().Events,
            item => item.EventType == "created" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.ActivationCodeId == body.DeviceArtifact.ActivationCodeId);
        Assert.Contains(
            factory.GetAdminEnrollmentAuditWriter().Events,
            item => item.EventType == "started" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.EnrollmentId == body.TotpEnrollment.EnrollmentId);

        var listResponse = await client.GetAsync(
            $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/device-onboarding-artifacts?externalUserId=user-combined");
        var listRaw = await listResponse.Content.ReadAsStringAsync();
        var currentResponse = await client.GetAsync(
            $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/users/user-combined/enrollments/totp/current");
        var currentRaw = await currentResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        Assert.DoesNotContain(body.ActivationPayload, listRaw, StringComparison.Ordinal);
        Assert.DoesNotContain(body.TotpEnrollment.SecretUri!, currentRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("secretUri", currentRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("qrCodePayload", currentRaw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePackage_ReturnsForbidden_WhenEnrollmentPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(
            client,
            csrfToken,
            "/api/v1/admin/combined-onboarding-packages",
            CreateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePackage_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.DevicesWrite, AdminPermissions.EnrollmentsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.PostAsJsonAsync("/api/v1/admin/combined-onboarding-packages", CreateRequest());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid anti-forgery token.", problem!.Title);
    }

    private static object CreateRequest()
    {
        return new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            externalUserId = "user-combined",
            platform = "android",
            ttlMinutes = 10,
            issuer = "OTPAuth",
            label = "user-combined",
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
