using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Tests.Enrollments;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminIntegrationClientApiTests
{
    [Fact]
    public async Task ListIntegrationClients_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync(ListPath());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListIntegrationClients_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.EnrollmentsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(ListPath());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListIntegrationClients_ReturnsBadRequest_WhenTenantIdIsEmpty()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync("/api/v1/admin/tenants/00000000-0000-0000-0000-000000000000/integration-clients");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid integration client lookup request.", problem!.Title);
        Assert.Equal("TenantId is required.", problem.Detail);
    }

    [Fact]
    public async Task ListIntegrationClients_ReturnsNotFound_WhenTenantHasNoClients()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(ListPath());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListIntegrationClients_ReturnsTenantScopedSanitizedClients()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsRead]);
        var store = factory.GetAdminIntegrationClients();
        var matchingClient = store.Seed(new AdminIntegrationClientView
        {
            ClientId = "otpauth-crm",
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            Status = AdminIntegrationClientStatus.Active,
            AllowedScopes = [IntegrationClientScopes.ChallengesWrite, IntegrationClientScopes.ChallengesRead],
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddHours(-12),
        });
        store.Seed(new AdminIntegrationClientView
        {
            ClientId = "otpauth-legacy",
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Status = AdminIntegrationClientStatus.Inactive,
            AllowedScopes = [IntegrationClientScopes.EnrollmentsWrite],
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        });
        store.Seed(new AdminIntegrationClientView
        {
            ClientId = "otpauth-other",
            TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ApplicationClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Status = AdminIntegrationClientStatus.Inactive,
            AllowedScopes = [IntegrationClientScopes.EnrollmentsWrite],
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-4),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddDays(-4),
        });
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.GetAsync(ListPath());
        var body = await response.Content.ReadFromJsonAsync<List<AdminIntegrationClientHttpResponse>>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body!.Count);
        var listedClient = body[0];
        Assert.Equal(matchingClient.ClientId, listedClient.ClientId);
        Assert.Equal(matchingClient.TenantId, listedClient.TenantId);
        Assert.Equal(matchingClient.ApplicationClientId, listedClient.ApplicationClientId);
        Assert.Equal("active", listedClient.Status);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
            listedClient.AllowedScopes);
        Assert.Equal(matchingClient.LastSecretRotatedUtc, listedClient.LastSecretRotatedUtc);
        Assert.Equal("inactive", body[1].Status);
        Assert.Equal("otpauth-legacy", body[1].ClientId);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("client_secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateIntegrationClient_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var response = await client.PostAsJsonAsync("/api/v1/admin/integration-clients", CreateRequest());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid anti-forgery token.", problem!.Title);
    }

    [Fact]
    public async Task CreateIntegrationClient_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/integration-clients", CreateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntegrationClient_ReturnsCreatedClientWithOneTimeSecret_AndWritesAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.IntegrationClientsRead, AdminPermissions.IntegrationClientsWrite]);
        var audit = factory.GetAdminIntegrationClientAuditWriter();
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/integration-clients", CreateRequest());
        var body = await response.Content.ReadFromJsonAsync<AdminCreateIntegrationClientHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("otpauth-new-client", body!.Client.ClientId);
        Assert.Equal(EnrollmentApiTestContext.TenantId, body.Client.TenantId);
        Assert.Equal(EnrollmentApiTestContext.ApplicationClientId, body.Client.ApplicationClientId);
        Assert.Equal("active", body.Client.Status);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
            body.Client.AllowedScopes);
        Assert.False(string.IsNullOrWhiteSpace(body.ClientSecret));
        Assert.DoesNotContain("client_secret_hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains(
            audit.Events,
            item => item.EventType == "created" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.ClientId == "otpauth-new-client" &&
                    item.TenantId == EnrollmentApiTestContext.TenantId &&
                    item.ApplicationClientId == EnrollmentApiTestContext.ApplicationClientId);

        var listResponse = await client.GetAsync(ListPath());
        var listBody = await listResponse.Content.ReadFromJsonAsync<List<AdminIntegrationClientHttpResponse>>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(listBody!, item => item.ClientId == "otpauth-new-client");
    }

    [Fact]
    public async Task CreateIntegrationClient_ReturnsBadRequest_WhenRequestContainsPlaintextSecret()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/integration-clients", new
        {
            clientId = "otpauth-new-client",
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            allowedScopes = new[] { IntegrationClientScopes.ChallengesRead },
            clientSecret = "operator-provided-secret",
        });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid integration client creation request.", problem!.Title);
    }

    [Fact]
    public async Task CreateIntegrationClient_ReturnsBadRequest_WhenScopeIsUnsupported()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/integration-clients", CreateRequest(
            allowedScopes: [IntegrationClientScopes.ChallengesRead, "unknown:scope"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntegrationClient_ReturnsConflict_WhenClientIdAlreadyExists()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        factory.GetAdminIntegrationClients().Seed(new AdminIntegrationClientView
        {
            ClientId = "otpauth-new-client",
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            Status = AdminIntegrationClientStatus.Active,
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
            CreatedUtc = DateTimeOffset.UtcNow,
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow,
        });
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/integration-clients", CreateRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RotateSecret_ReturnsOneTimeSecret_AndInvalidatesAuthState()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.IntegrationClientsWrite]);
        var existing = SeedClient(factory, status: AdminIntegrationClientStatus.Active);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, ClientActionPath(existing.ClientId, "rotate-secret"), new { });
        var body = await response.Content.ReadFromJsonAsync<AdminRotateIntegrationClientSecretHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(existing.ClientId, body!.Client.ClientId);
        Assert.False(string.IsNullOrWhiteSpace(body.ClientSecret));
        Assert.True(body.Client.LastAuthStateChangedUtc > existing.LastAuthStateChangedUtc);
        Assert.Equal(body.Client.LastAuthStateChangedUtc, body.Client.LastSecretRotatedUtc);
        Assert.DoesNotContain("client_secret_hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains(
            factory.GetAdminIntegrationClientAuditWriter().Events,
            item => item.EventType == "secret_rotated" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.ClientId == existing.ClientId);
    }

    [Fact]
    public async Task UpdateScopes_RejectsUnsupportedScopes()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        var existing = SeedClient(factory, status: AdminIntegrationClientStatus.Active);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PutWithCsrfAsync(client, csrfToken, ClientActionPath(existing.ClientId, "scopes"), new
        {
            allowedScopes = new[] { IntegrationClientScopes.ChallengesRead, "unknown:scope" },
        });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid integration client scope update request.", problem!.Title);
    }

    [Fact]
    public async Task UpdateScopes_ReturnsSanitizedClient_AndInvalidatesAuthState()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.IntegrationClientsWrite]);
        var existing = SeedClient(factory, status: AdminIntegrationClientStatus.Active);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PutWithCsrfAsync(client, csrfToken, ClientActionPath(existing.ClientId, "scopes"), new
        {
            allowedScopes = new[] { IntegrationClientScopes.DevicesWrite, IntegrationClientScopes.ChallengesRead },
        });
        var body = await response.Content.ReadFromJsonAsync<AdminIntegrationClientHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.DevicesWrite],
            body!.AllowedScopes);
        Assert.True(body.LastAuthStateChangedUtc > existing.LastAuthStateChangedUtc);
        Assert.Contains(
            factory.GetAdminIntegrationClientAuditWriter().Events,
            item => item.EventType == "scopes_changed" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.ClientId == existing.ClientId);
    }

    [Fact]
    public async Task Deactivate_ReturnsConflict_WhenClientIsAlreadyInactive()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        var existing = SeedClient(factory, status: AdminIntegrationClientStatus.Inactive);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, ClientActionPath(existing.ClientId, "deactivate"), new { });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Integration client state cannot be changed.", problem!.Title);
    }

    [Fact]
    public async Task DeactivateAndReactivate_ReturnSanitizedStateChanges()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        var existing = SeedClient(factory, status: AdminIntegrationClientStatus.Active);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var deactivateResponse = await PostWithCsrfAsync(client, csrfToken, ClientActionPath(existing.ClientId, "deactivate"), new { });
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<AdminIntegrationClientHttpResponse>();
        var reactivateResponse = await PostWithCsrfAsync(client, csrfToken, ClientActionPath(existing.ClientId, "reactivate"), new { });
        var reactivated = await reactivateResponse.Content.ReadFromJsonAsync<AdminIntegrationClientHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.NotNull(deactivated);
        Assert.Equal("inactive", deactivated!.Status);
        Assert.True(deactivated.LastAuthStateChangedUtc > existing.LastAuthStateChangedUtc);
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        Assert.NotNull(reactivated);
        Assert.Equal("active", reactivated!.Status);
        Assert.True(reactivated.LastAuthStateChangedUtc > deactivated.LastAuthStateChangedUtc);
        Assert.Contains(factory.GetAdminIntegrationClientAuditWriter().Events, item => item.EventType == "deactivated");
        Assert.Contains(factory.GetAdminIntegrationClientAuditWriter().Events, item => item.EventType == "reactivated");
    }

    [Fact]
    public async Task LifecycleCommands_ReturnNotFound_ForCrossTenantRoute()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsWrite]);
        var existing = SeedClient(factory, status: AdminIntegrationClientStatus.Active);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client, "operator", "super-secret");
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(
            client,
            csrfToken,
            $"/api/v1/admin/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/integration-clients/{existing.ClientId}/rotate-secret",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string ListPath()
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/integration-clients";
    }

    private static string ClientActionPath(string clientId, string action)
    {
        return $"/api/v1/admin/tenants/{EnrollmentApiTestContext.TenantId}/integration-clients/{clientId}/{action}";
    }

    private static AdminIntegrationClientView SeedClient(
        AdminAuthApiTestFactory factory,
        AdminIntegrationClientStatus status)
    {
        return factory.GetAdminIntegrationClients().Seed(new AdminIntegrationClientView
        {
            ClientId = "otpauth-managed-client",
            TenantId = EnrollmentApiTestContext.TenantId,
            ApplicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            Status = status,
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        });
    }

    private static object CreateRequest(IReadOnlyCollection<string>? allowedScopes = null)
    {
        return new
        {
            clientId = "otpauth-new-client",
            tenantId = EnrollmentApiTestContext.TenantId,
            applicationClientId = EnrollmentApiTestContext.ApplicationClientId,
            allowedScopes = allowedScopes ?? [IntegrationClientScopes.ChallengesWrite, IntegrationClientScopes.ChallengesRead],
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

    private static async Task<HttpResponseMessage> PutWithCsrfAsync(
        HttpClient client,
        string csrfToken,
        string uri,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }
}
