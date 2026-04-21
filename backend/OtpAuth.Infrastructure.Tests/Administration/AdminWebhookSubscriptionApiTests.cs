using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Webhooks;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminWebhookSubscriptionApiTests
{
    [Fact]
    public async Task ListSubscriptions_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/webhook-subscriptions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListSubscriptions_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync($"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/webhook-subscriptions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListSubscriptions_ReturnsTenantScopedSubscriptions()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksRead]);
        var store = factory.GetWebhookSubscriptions();
        store.Seed(new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/platform"),
            IsActive = true,
            EventTypes = [WebhookEventTypeNames.ChallengeApproved, WebhookEventTypeNames.DeviceActivated],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        store.Seed(new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ApplicationClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            EndpointUrl = new Uri("https://erp.example.com/webhooks/platform"),
            IsActive = true,
            EventTypes = [WebhookEventTypeNames.FactorRevoked],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync($"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/webhook-subscriptions");
        var body = await response.Content.ReadFromJsonAsync<List<AdminWebhookSubscriptionHttpResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subscription = Assert.Single(body!);
        Assert.Equal("https://crm.example.com/webhooks/platform", subscription.EndpointUrl);
        Assert.Equal("active", subscription.Status);
        Assert.Equal(
            [WebhookEventTypeNames.ChallengeApproved, WebhookEventTypeNames.DeviceActivated],
            subscription.EventTypes);
    }

    [Fact]
    public async Task UpsertSubscription_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.PostAsJsonAsync("/api/v1/admin/webhook-subscriptions", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            endpointUrl = "https://crm.example.com/webhooks/platform",
            eventTypes = new[] { WebhookEventTypeNames.ChallengeApproved },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSubscription_CreatesSubscription_AndWritesAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.WebhooksRead, AdminPermissions.WebhooksWrite]);
        var auditWriter = factory.GetAdminWebhookSubscriptionAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/webhook-subscriptions", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            endpointUrl = "https://crm.example.com/webhooks/platform",
            eventTypes = new[] { WebhookEventTypeNames.ChallengeApproved, WebhookEventTypeNames.DeviceActivated },
        });
        var body = await response.Content.ReadFromJsonAsync<AdminWebhookSubscriptionHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(EnrollmentApiTestContext.ApplicationClientId, body!.ApplicationClientId);
        Assert.Equal("active", body.Status);
        Assert.Equal(
            [WebhookEventTypeNames.ChallengeApproved, WebhookEventTypeNames.DeviceActivated],
            body.EventTypes);
        Assert.Contains(
            auditWriter.Events,
            audit => audit.AdminUserId == adminUser.AdminUserId &&
                     audit.SubscriptionId == body.SubscriptionId &&
                     audit.IsActive);
    }

    [Fact]
    public async Task UpsertSubscription_ReturnsConflict_WhenTenantHasMultipleApplicationClients()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksWrite]);
        factory.GetIntegrationClients().Seed(
            "otpauth-second",
            EnrollmentApiTestContext.TenantId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"));
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/webhook-subscriptions", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            endpointUrl = "https://crm.example.com/webhooks/platform",
            eventTypes = new[] { WebhookEventTypeNames.ChallengeApproved },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSubscription_CanDeactivateExistingSubscription()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksRead, AdminPermissions.WebhooksWrite]);
        var existing = factory.GetWebhookSubscriptions().Seed(new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/platform"),
            IsActive = true,
            EventTypes = [WebhookEventTypeNames.ChallengeApproved],
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/webhook-subscriptions", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            endpointUrl = existing.EndpointUrl.ToString(),
            eventTypes = new[] { WebhookEventTypeNames.ChallengeApproved },
            isActive = false,
        });
        var body = await response.Content.ReadFromJsonAsync<AdminWebhookSubscriptionHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("inactive", body!.Status);
        var listed = await client.GetFromJsonAsync<List<AdminWebhookSubscriptionHttpResponse>>(
            $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/webhook-subscriptions");
        var subscription = Assert.Single(listed!);
        Assert.Equal("inactive", subscription.Status);
    }

    [Fact]
    public async Task UpsertSubscription_ReturnsBadRequest_WhenEndpointIsPrivateNetwork()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksWrite]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/webhook-subscriptions", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            endpointUrl = "https://127.0.0.1/webhooks",
            eventTypes = new[] { WebhookEventTypeNames.ChallengeApproved },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string csrfToken, string uri, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }
}
