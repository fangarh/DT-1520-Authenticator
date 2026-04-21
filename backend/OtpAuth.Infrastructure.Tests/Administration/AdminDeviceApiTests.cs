using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeviceApiTests
{
    [Fact]
    public async Task ListDevices_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync(ListPath("user-list"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDevices_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(ListPath("user-list"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListDevices_ReturnsSanitizedCurrentAndRecentDevices()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesRead]);
        var devices = factory.GetDevices();
        var activeDevice = devices.SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-list",
            "installation-active",
            pushToken: "push-token",
            deviceId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
        devices.SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-list",
            "installation-revoked",
            status: DeviceStatus.Revoked,
            pushToken: null,
            deviceId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        devices.SeedActiveDevice(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EnrollmentApiTestContext.ApplicationClientId,
            "user-list",
            "installation-other-tenant",
            deviceId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(ListPath("user-list"));
        var body = await response.Content.ReadFromJsonAsync<List<AdminUserDeviceHttpResponse>>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body!.Count);
        Assert.Equal(activeDevice.Device.Id, body[0].DeviceId);
        Assert.Equal("active", body[0].Status);
        Assert.True(body[0].IsPushCapable);
        Assert.Equal("revoked", body[1].Status);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("deviceName", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publicKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pushToken", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevokeDevice_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var seeded = factory.GetDevices().SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-revoke",
            "installation-csrf");
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.PostAsync(RevokePath("user-revoke", seeded.Device.Id), content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevokeDevice_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var seeded = factory.GetDevices().SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-revoke",
            "installation-forbidden");
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesRead]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithoutBodyWithCsrfAsync(client, csrfToken, RevokePath("user-revoke", seeded.Device.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokeDevice_ReturnsNotFound_WhenDeviceDoesNotBelongToRequestedUser()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var seeded = factory.GetDevices().SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-revoke",
            "installation-not-found");
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithoutBodyWithCsrfAsync(client, csrfToken, RevokePath("user-other", seeded.Device.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeDevice_ReturnsConflict_WhenDeviceIsAlreadyRevoked()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var seeded = factory.GetDevices().SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-revoke",
            "installation-conflict",
            status: DeviceStatus.Revoked);
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesWrite]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithoutBodyWithCsrfAsync(client, csrfToken, RevokePath("user-revoke", seeded.Device.Id));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Device cannot be revoked.", problem!.Title);
    }

    [Fact]
    public async Task RevokeDevice_ReturnsRevokedDevice_AndWritesLifecycleAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var seeded = factory.GetDevices().SeedActiveDevice(
            EnrollmentApiTestContext.TenantId,
            EnrollmentApiTestContext.ApplicationClientId,
            "user-revoke",
            "installation-success");
        factory.GetDevices().SeedWebhookSubscription(new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = seeded.Device.TenantId,
            ApplicationClientId = seeded.Device.ApplicationClientId,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/devices"),
            IsActive = true,
            EventTypes = [WebhookEventTypeNames.DeviceRevoked],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.DevicesRead, AdminPermissions.DevicesWrite]);
        var audit = factory.GetDeviceAuditWriter();
        var adminAudit = factory.GetAdminDeviceAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithoutBodyWithCsrfAsync(client, csrfToken, RevokePath("user-revoke", seeded.Device.Id));
        var body = await response.Content.ReadFromJsonAsync<AdminUserDeviceHttpResponse>();
        var listResponse = await client.GetAsync(ListPath("user-revoke"));
        var listBody = await listResponse.Content.ReadFromJsonAsync<List<AdminUserDeviceHttpResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("revoked", body!.Status);
        Assert.Contains(audit.Events, entry => entry == $"revoked:{seeded.Device.Id}:True");
        Assert.Contains(
            adminAudit.Events,
            entry => entry.DeviceId == seeded.Device.Id &&
                     entry.Status == "revoked" &&
                     entry.IsPushCapable);
        Assert.NotNull(listBody);
        Assert.Equal("revoked", Assert.Single(listBody!, device => device.DeviceId == seeded.Device.Id).Status);
        var delivery = Assert.Single(factory.GetDevices().GetWebhookDeliveries());
        Assert.Equal(WebhookEventTypeNames.DeviceRevoked, delivery.EventType);
        Assert.Equal(seeded.Device.Id, delivery.ResourceId);
    }

    private static string ListPath(string externalUserId)
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/users/{externalUserId}/devices";
    }

    private static string RevokePath(string externalUserId, Guid deviceId)
    {
        return $"{ListPath(externalUserId)}/{deviceId}/revoke";
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
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
        return await GetCsrfTokenAsync(client);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/admin/auth/csrf-token");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AdminCsrfTokenHttpResponse>();
        Assert.NotNull(body);
        return body!.RequestToken;
    }

    private static async Task<HttpResponseMessage> PostWithoutBodyWithCsrfAsync(HttpClient client, string csrfToken, string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }
}
