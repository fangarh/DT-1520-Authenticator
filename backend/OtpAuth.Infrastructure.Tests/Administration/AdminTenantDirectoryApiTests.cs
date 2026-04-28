using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OtpAuth.Api.Admin;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminTenantDirectoryApiTests
{
    [Fact]
    public async Task ListTenants_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new AdminAuthApiTestFactory();
        using var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/admin/tenants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListTenants_ReturnsForbidden_WhenPermissionIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.IntegrationClientsRead]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var response = await client.GetAsync("/api/v1/admin/tenants");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListTenants_ReturnsSanitizedDirectorySummaries()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.TenantsRead]);
        var tenant = SeedTenant(factory);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var response = await client.GetAsync("/api/v1/admin/tenants");
        var body = await response.Content.ReadFromJsonAsync<List<AdminTenantDirectoryTenantHttpResponse>>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body!);
        Assert.Equal(tenant.Tenant.TenantId, body[0].TenantId);
        Assert.Equal("active", body[0].Status);
        Assert.Equal(1, body[0].ApplicationCount);
        Assert.Equal(1, body[0].IntegrationClientCount);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("clientSecret", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTenantDirectory_ReturnsApplicationsAndSanitizedClients()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.TenantsRead]);
        var tenant = SeedTenant(factory);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var response = await client.GetAsync($"/api/v1/admin/tenants/{tenant.Tenant.TenantId:D}/directory");
        var body = await response.Content.ReadFromJsonAsync<AdminTenantDirectoryDetailHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(tenant.Tenant.TenantId, body!.Tenant.TenantId);
        Assert.Single(body.Applications);
        Assert.Single(body.IntegrationClients);
        Assert.Equal("directory-client", body.IntegrationClients.Single().ClientId);
        Assert.DoesNotContain("clientSecret", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTenant_ReturnsBadRequest_WhenCsrfTokenIsMissing()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.TenantsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/admin/tenants", CreateTenantRequest());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid anti-forgery token.", problem!.Title);
    }

    [Fact]
    public async Task CreateTenant_ReturnsCreatedManualTenant_AndWritesAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.TenantsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/tenants", CreateTenantRequest());
        var body = await response.Content.ReadFromJsonAsync<AdminTenantDirectoryTenantHttpResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(Guid.Parse("30303030-3030-3030-3030-303030303030"), body!.TenantId);
        Assert.Equal("Migration Tenant", body.DisplayName);
        Assert.Equal("test", body.Status);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains(
            factory.GetAdminTenantDirectoryAuditWriter().Events,
            item => item.EventType == "created" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.TenantId == body.TenantId);
    }

    [Fact]
    public async Task CreateTenant_ReturnsConflict_WhenNameAlreadyExists()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.TenantsWrite]);
        SeedTenant(factory);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/tenants", new
        {
            displayName = "Directory Tenant",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task QuickCreateTenant_ReturnsOneTimeClientSecret_AndWritesAudit()
    {
        await using var factory = new AdminAuthApiTestFactory();
        var adminUser = factory.GetAdminUsers().Seed(
            "operator",
            "super-secret",
            permissions: [AdminPermissions.TenantsRead, AdminPermissions.TenantsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/tenants/quick-create", QuickCreateRequest());
        var body = await response.Content.ReadFromJsonAsync<AdminQuickCreateTenantHttpResponse>();
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(Guid.Parse("10101010-1010-1010-1010-101010101010"), body!.Directory.Tenant.TenantId);
        Assert.Equal(Guid.Parse("20202020-2020-2020-2020-202020202020"), body.Directory.Applications.Single().ApplicationClientId);
        Assert.Equal("generated-client", body.Client.ClientId);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.DevicesWrite],
            body.Client.AllowedScopes);
        Assert.False(string.IsNullOrWhiteSpace(body.ClientSecret));
        Assert.DoesNotContain("client_secret_hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains(
            factory.GetAdminTenantDirectoryAuditWriter().Events,
            item => item.EventType == "quick_created" &&
                    item.AdminUserId == adminUser.AdminUserId &&
                    item.TenantId == body.Directory.Tenant.TenantId &&
                    item.ClientId == "generated-client");
    }

    [Fact]
    public async Task QuickCreateTenant_ReturnsBadRequest_WhenRequestContainsPlaintextSecret()
    {
        await using var factory = new AdminAuthApiTestFactory();
        factory.GetAdminUsers().Seed("operator", "super-secret", permissions: [AdminPermissions.TenantsWrite]);
        using var client = factory.CreateAdminClient();

        await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        var response = await PostWithCsrfAsync(client, csrfToken, "/api/v1/admin/tenants/quick-create", new
        {
            tenantDisplayName = "Example Tenant",
            applicationDisplayName = "Project Manager",
            integrationClientDisplayName = "Backend API",
            allowedScopes = new[] { IntegrationClientScopes.ChallengesRead },
            clientSecret = "operator-provided-secret",
        });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid tenant quick-create request.", problem!.Title);
    }

    private static AdminTenantDirectoryDetailView SeedTenant(AdminAuthApiTestFactory factory)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var applicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        return factory.GetAdminTenants().Seed(
            new AdminTenantDirectoryTenantView
            {
                TenantId = tenantId,
                DisplayName = "Directory Tenant",
                Slug = "directory-tenant",
                Status = AdminTenantDirectoryStatus.Active,
                ApplicationCount = 1,
                IntegrationClientCount = 1,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            },
            [
                new AdminTenantDirectoryApplicationView
                {
                    ApplicationClientId = applicationClientId,
                    TenantId = tenantId,
                    DisplayName = "Project Manager",
                    Slug = "project-manager",
                    Status = AdminTenantDirectoryStatus.Active,
                    IntegrationClientCount = 1,
                    CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                    UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                },
            ],
            [
                new AdminIntegrationClientView
                {
                    ClientId = "directory-client",
                    TenantId = tenantId,
                    ApplicationClientId = applicationClientId,
                    Status = AdminIntegrationClientStatus.Active,
                    AllowedScopes = [IntegrationClientScopes.ChallengesRead],
                    CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                    UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                },
            ]);
    }

    private static object CreateTenantRequest()
    {
        return new
        {
            tenantId = Guid.Parse("30303030-3030-3030-3030-303030303030"),
            displayName = "Migration Tenant",
            slug = "migration-tenant",
            status = "test",
        };
    }

    private static object QuickCreateRequest()
    {
        return new
        {
            tenantDisplayName = "Example Tenant",
            applicationDisplayName = "Project Manager",
            integrationClientDisplayName = "Backend API",
            allowedScopes = new[] { IntegrationClientScopes.DevicesWrite, IntegrationClientScopes.ChallengesRead },
        };
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = "operator",
                password = "super-secret",
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
