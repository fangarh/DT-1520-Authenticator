using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminCurrentEnrollmentApiTests
{
    [Fact]
    public async Task GetCurrentEnrollment_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync(CurrentEnrollmentPath("user-123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentEnrollment_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret");
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        var loginResponse = await LoginAsync(client, csrfToken, "operator", "super-secret");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync(CurrentEnrollmentPath("user-123"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentEnrollment_ReturnsNotFound_WhenUserIsUnknown()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        var loginResponse = await LoginAsync(client, csrfToken, "operator", "super-secret");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync(CurrentEnrollmentPath("missing-user"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentEnrollment_ReturnsCurrentSummary_WithoutProvisioningArtifacts()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        var store = factory.GetEnrollments();
        var enrollment = store.SeedConfirmed(
            "user-current",
            pendingReplacement: new Application.Enrollments.TotpPendingReplacementRecord
            {
                Secret = [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29],
                Digits = 6,
                PeriodSeconds = 30,
                Algorithm = "SHA1",
                StartedUtc = DateTimeOffset.UtcNow,
                FailedConfirmationAttempts = 0,
            });
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        var loginResponse = await LoginAsync(client, csrfToken, "operator", "super-secret");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync(CurrentEnrollmentPath("user-current"));
        var body = await response.Content.ReadFromJsonAsync<AdminTotpEnrollmentCurrentHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(enrollment.EnrollmentId, body!.EnrollmentId);
        Assert.Equal(EnrollmentApiTestContext.TenantId, body.TenantId);
        Assert.Equal(EnrollmentApiTestContext.ApplicationClientId, body.ApplicationClientId);
        Assert.Equal("user-current", body.ExternalUserId);
        Assert.Equal("confirmed", body.Status);
        Assert.True(body.HasPendingReplacement);
        Assert.NotNull(body.ConfirmedAtUtc);
        Assert.Null(body.RevokedAtUtc);
        Assert.DoesNotContain("secretUri", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("qrCodePayload", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentEnrollment_ReturnsRevokedState_WithRevokedTimestamp()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        var store = factory.GetEnrollments();
        store.SeedRevoked("user-revoked");
        using var client = factory.CreateAdminClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        var loginResponse = await LoginAsync(client, csrfToken, "operator", "super-secret");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync(CurrentEnrollmentPath("user-revoked"));
        var body = await response.Content.ReadFromJsonAsync<AdminTotpEnrollmentCurrentHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("revoked", body!.Status);
        Assert.NotNull(body.RevokedAtUtc);
    }

    private static string CurrentEnrollmentPath(string externalUserId)
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/users/{externalUserId}/enrollments/totp/current";
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/admin/auth/csrf-token");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AdminCsrfTokenHttpResponse>();
        Assert.NotNull(body);
        return body!.RequestToken;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string csrfToken, string username, string password)
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
