using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeliveryStatusApiTests
{
    [Fact]
    public async Task ListDeliveryStatuses_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync(ListPath());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDeliveryStatuses_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(ListPath());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListDeliveryStatuses_ReturnsBadRequest_WhenChannelFilterIsInvalid()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync($"{ListPath()}?channel=push");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid delivery status filter.", problem!.Title);
        Assert.Equal("Channel must be one of: challenge_callback, webhook_event.", problem.Detail);
    }

    [Fact]
    public async Task ListDeliveryStatuses_ReturnsNotFound_WhenApplicationClientDoesNotBelongToTenant()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(
            $"{ListPath()}?applicationClientId={Guid.Parse("33333333-3333-3333-3333-333333333333")}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListDeliveryStatuses_ReturnsFilteredSanitizedDeliveries()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.WebhooksRead]);
        var store = factory.GetAdminDeliveryStatuses();
        var matchingDelivery = store.Seed(new AdminDeliveryStatusView
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            Channel = AdminDeliveryChannel.WebhookEvent,
            Status = AdminDeliveryStatus.Failed,
            EventType = "device.blocked",
            DeliveryDestination = "https://operator:secret@crm.example.com/webhooks/platform?token=abc#fragment",
            SubjectType = "device",
            SubjectId = Guid.NewGuid(),
            PublicationId = Guid.NewGuid(),
            AttemptCount = 3,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            LastAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            DeliveredAtUtc = null,
            LastErrorCode = "delivery_failed",
        });
        store.Seed(new AdminDeliveryStatusView
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            Channel = AdminDeliveryChannel.ChallengeCallback,
            Status = AdminDeliveryStatus.Delivered,
            EventType = "challenge.approved",
            DeliveryDestination = "https://crm.example.com/callbacks/approved",
            SubjectType = "challenge",
            SubjectId = Guid.NewGuid(),
            AttemptCount = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            DeliveredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
        });
        store.Seed(new AdminDeliveryStatusView
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ApplicationClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Channel = AdminDeliveryChannel.WebhookEvent,
            Status = AdminDeliveryStatus.Failed,
            EventType = "device.revoked",
            DeliveryDestination = "https://erp.example.com/webhooks/platform",
            SubjectType = "device",
            SubjectId = Guid.NewGuid(),
            AttemptCount = 2,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(2),
            LastAttemptAtUtc = DateTimeOffset.UtcNow,
            LastErrorCode = "delivery_failed",
        });
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(
            $"{ListPath()}?applicationClientId={EnrollmentApiTestContext.ApplicationClientId}&channel=webhook_event&status=failed&limit=1");
        var body = await response.Content.ReadFromJsonAsync<List<AdminDeliveryStatusHttpResponse>>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var delivery = Assert.Single(body!);
        Assert.Equal(matchingDelivery.DeliveryId, delivery.DeliveryId);
        Assert.Equal("webhook_event", delivery.Channel);
        Assert.Equal("failed", delivery.Status);
        Assert.Equal("device.blocked", delivery.EventType);
        Assert.Equal("https://crm.example.com/webhooks/platform", delivery.DeliveryDestination);
        Assert.False(delivery.IsRetryScheduled);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=abc", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fragment", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static string ListPath()
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/delivery-statuses";
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
