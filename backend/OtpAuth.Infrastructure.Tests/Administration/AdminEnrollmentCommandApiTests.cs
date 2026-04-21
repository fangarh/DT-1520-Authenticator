using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Webhooks;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminEnrollmentCommandApiTests
{
    [Fact]
    public async Task StartEnrollment_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            externalUserId = "user-start",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartEnrollment_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");

        var response = await client.PostAsJsonAsync("/api/v1/admin/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            externalUserId = "user-start",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartEnrollment_CreatesProvisioningArtifact_AndUsesResolvedApplicationClient()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsWrite]);
        var adminAudit = factory.GetAdminEnrollmentAuditWriter();
        var enrollmentAudit = factory.GetTotpEnrollmentAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            externalUserId = "user-start",
            issuer = "OTPAuth",
            label = "ivan.petrov",
        });
        var body = await response.Content.ReadFromJsonAsync<AdminTotpEnrollmentCommandHttpResponse>();
        var currentResponse = await client.GetAsync($"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/users/user-start/enrollments/totp/current");
        var currentBody = await currentResponse.Content.ReadFromJsonAsync<AdminTotpEnrollmentCurrentHttpResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending", body!.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.SecretUri));
        Assert.Equal($"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/users/user-start/enrollments/totp/current", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        Assert.NotNull(currentBody);
        Assert.Equal(EnrollmentApiTestContext.ApplicationClientId, currentBody!.ApplicationClientId);
        Assert.Contains(adminAudit.Events, audit => audit.EventType == "started" && audit.EnrollmentId == body.EnrollmentId);
        Assert.Contains(enrollmentAudit.Events, audit => audit.EventType == "started" && audit.EnrollmentId == body.EnrollmentId);
    }

    [Fact]
    public async Task StartEnrollment_ReturnsConflict_WhenMultipleActiveApplicationClientsExist()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsWrite]);
        factory.GetIntegrationClients().Seed(
            "otpauth-second",
            EnrollmentApiTestContext.TenantId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"));
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            externalUserId = "user-start",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StartEnrollment_UsesExplicitApplicationClient_WhenMultipleClientsExist()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsWrite]);
        var explicitApplicationClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        factory.GetIntegrationClients().Seed("otpauth-second", EnrollmentApiTestContext.TenantId, explicitApplicationClientId);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = explicitApplicationClientId,
            externalUserId = "user-explicit",
        });
        var currentResponse = await client.GetAsync($"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/users/user-explicit/enrollments/totp/current");
        var currentBody = await currentResponse.Content.ReadFromJsonAsync<AdminTotpEnrollmentCurrentHttpResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(currentBody);
        Assert.Equal(explicitApplicationClientId, currentBody!.ApplicationClientId);
    }

    [Fact]
    public async Task ReplaceEnrollment_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var enrollment = factory.GetEnrollments().SeedConfirmed();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/enrollments/totp/{enrollment.EnrollmentId}/replace");
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEnrollment_ReturnsNotFound_WhenEnrollmentDoesNotExist()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsWrite]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithCsrfAsync(client, csrfToken, $"/api/v1/admin/enrollments/totp/{Guid.NewGuid()}/confirm", new
        {
            code = "123456",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceAndConfirmEnrollment_ReturnProvisioningArtifact_AndWriteAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var enrollment = factory.GetEnrollments().SeedConfirmed("user-replace");
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsWrite]);
        var adminAudit = factory.GetAdminEnrollmentAuditWriter();
        var enrollmentAudit = factory.GetTotpEnrollmentAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var replaceResponse = await PostWithoutBodyWithCsrfAsync(client, csrfToken, $"/api/v1/admin/enrollments/totp/{enrollment.EnrollmentId}/replace");
        var replaceBody = await replaceResponse.Content.ReadFromJsonAsync<AdminTotpEnrollmentCommandHttpResponse>();
        var code = GenerateCurrentCode(replaceBody!.SecretUri!);
        var confirmResponse = await PostWithCsrfAsync(client, csrfToken, $"/api/v1/admin/enrollments/totp/{enrollment.EnrollmentId}/confirm", new
        {
            code,
        });
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<AdminTotpEnrollmentCommandHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        Assert.NotNull(replaceBody);
        Assert.True(replaceBody!.HasPendingReplacement);
        Assert.False(string.IsNullOrWhiteSpace(replaceBody.SecretUri));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.NotNull(confirmBody);
        Assert.Equal("confirmed", confirmBody!.Status);
        Assert.False(confirmBody.HasPendingReplacement);
        Assert.Contains(adminAudit.Events, audit => audit.EventType == "replacement_started" && audit.EnrollmentId == enrollment.EnrollmentId);
        Assert.Contains(adminAudit.Events, audit => audit.EventType == "replacement_confirmed" && audit.EnrollmentId == enrollment.EnrollmentId);
        Assert.Contains(enrollmentAudit.Events, audit => audit.EventType == "replacement_started" && audit.EnrollmentId == enrollment.EnrollmentId);
        Assert.Contains(enrollmentAudit.Events, audit => audit.EventType == "replacement_confirmed" && audit.EnrollmentId == enrollment.EnrollmentId);
    }

    [Fact]
    public async Task RevokeEnrollment_ReturnsRevokedState_AndWritesAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var enrollment = factory.GetEnrollments().SeedConfirmed(
            "user-revoke",
            new TotpPendingReplacementRecord
            {
                Secret = [21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40],
                Digits = 6,
                PeriodSeconds = 30,
                Algorithm = "SHA1",
                StartedUtc = DateTimeOffset.UtcNow,
                FailedConfirmationAttempts = 1,
            });
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsWrite]);
        var adminAudit = factory.GetAdminEnrollmentAuditWriter();
        var enrollmentAudit = factory.GetTotpEnrollmentAuditWriter();
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithoutBodyWithCsrfAsync(client, csrfToken, $"/api/v1/admin/enrollments/totp/{enrollment.EnrollmentId}/revoke");
        var body = await response.Content.ReadFromJsonAsync<AdminTotpEnrollmentCommandHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("revoked", body!.Status);
        Assert.False(body.HasPendingReplacement);
        Assert.Contains(adminAudit.Events, audit => audit.EventType == "revoked" && audit.EnrollmentId == enrollment.EnrollmentId);
        Assert.Contains(enrollmentAudit.Events, audit => audit.EventType == "revoked" && audit.EnrollmentId == enrollment.EnrollmentId);
    }

    [Fact]
    public async Task RevokeEnrollment_EnqueuesFactorRevokedWebhook_ForMatchingSubscription()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var enrollment = factory.GetEnrollments().SeedConfirmed("user-admin-factor-webhook");
        factory.GetEnrollments().SeedWebhookSubscription(new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = enrollment.TenantId,
            ApplicationClientId = enrollment.ApplicationClientId,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/factors"),
            IsActive = true,
            EventTypes = [WebhookEventTypeNames.FactorRevoked],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsWrite]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await LoginAsync(client, "operator", "super-secret");
        var response = await PostWithoutBodyWithCsrfAsync(client, csrfToken, $"/api/v1/admin/enrollments/totp/{enrollment.EnrollmentId}/revoke");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var delivery = Assert.Single(factory.GetEnrollments().GetWebhookDeliveries());
        Assert.Equal(WebhookEventTypeNames.FactorRevoked, delivery.EventType);
        Assert.Equal(WebhookResourceTypeNames.Factor, delivery.ResourceType);
        Assert.Equal(enrollment.EnrollmentId, delivery.ResourceId);
        Assert.Contains("\"externalUserId\":\"user-admin-factor-webhook\"", delivery.PayloadJson);
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

    private static async Task<HttpResponseMessage> PostWithoutBodyWithCsrfAsync(HttpClient client, string csrfToken, string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private static string GenerateCurrentCode(string secretUri)
    {
        var parameters = ParseQueryParameters(secretUri);
        var secret = Base32Decode(parameters["secret"]);
        var digits = int.Parse(parameters["digits"]);
        var period = int.Parse(parameters["period"]);
        var algorithm = parameters["algorithm"];
        var timeStep = TotpCodeCalculator.GetTimeStep(DateTimeOffset.UtcNow, period);

        return TotpCodeCalculator.GenerateCode(secret, digits, algorithm, timeStep);
    }

    private static Dictionary<string, string> ParseQueryParameters(string uri)
    {
        var query = new Uri(uri).Query.TrimStart('?');
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            parameters[key] = value;
        }

        return parameters;
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        var normalized = encoded.TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in normalized)
        {
            var value = alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new InvalidOperationException($"Unexpected base32 character '{character}'.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. bytes];
    }
}
