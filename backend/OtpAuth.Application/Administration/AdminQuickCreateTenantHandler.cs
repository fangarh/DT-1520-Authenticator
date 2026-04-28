using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Administration;

public sealed class AdminQuickCreateTenantHandler
{
    private readonly IAdminTenantDirectoryStore _store;
    private readonly IClientSecretHasher _clientSecretHasher;
    private readonly IAdminIntegrationClientSecretGenerator _secretGenerator;
    private readonly IAdminTenantDirectoryIdGenerator _idGenerator;
    private readonly IAdminTenantDirectoryAuditWriter _auditWriter;

    public AdminQuickCreateTenantHandler(
        IAdminTenantDirectoryStore store,
        IClientSecretHasher clientSecretHasher,
        IAdminIntegrationClientSecretGenerator secretGenerator,
        IAdminTenantDirectoryIdGenerator idGenerator,
        IAdminTenantDirectoryAuditWriter auditWriter)
    {
        _store = store;
        _clientSecretHasher = clientSecretHasher;
        _secretGenerator = secretGenerator;
        _idGenerator = idGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<AdminQuickCreateTenantResult> HandleAsync(
        AdminTenantQuickCreateRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.TenantsWrite))
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.TenantsWrite}' is required.");
        }

        if (request.HasOperatorProvidedSecret)
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.ValidationFailed,
                "Tenant quick create generates integration client secrets server-side.");
        }

        var tenantDisplayName = AdminTenantDirectoryValidation.NormalizeDisplayName(request.TenantDisplayName);
        if (tenantDisplayName is null)
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.ValidationFailed,
                "Tenant display name must be 1-200 characters.");
        }

        var applicationDisplayName = AdminTenantDirectoryValidation.NormalizeDisplayName(request.ApplicationDisplayName);
        if (applicationDisplayName is null)
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.ValidationFailed,
                "Application display name must be 1-200 characters.");
        }

        var clientDisplayName = AdminTenantDirectoryValidation.NormalizeDisplayName(request.IntegrationClientDisplayName);
        if (clientDisplayName is null)
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.ValidationFailed,
                "Integration client display name must be 1-200 characters.");
        }

        var normalizedScopes = AdminIntegrationClientValidation.NormalizeScopes(request.AllowedScopes, out var scopeError);
        if (scopeError is not null)
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.ValidationFailed,
                scopeError);
        }

        var clientSecret = _secretGenerator.Generate();
        var now = DateTimeOffset.UtcNow;
        var draft = new AdminTenantQuickCreateDraft
        {
            TenantId = _idGenerator.NewTenantId(),
            ApplicationClientId = _idGenerator.NewApplicationClientId(),
            TenantDisplayName = tenantDisplayName,
            TenantSlug = AdminTenantDirectoryValidation.CreateSlugCandidate(tenantDisplayName, "tenant"),
            ApplicationDisplayName = applicationDisplayName,
            ApplicationSlug = AdminTenantDirectoryValidation.CreateSlugCandidate(applicationDisplayName, "application"),
            ClientId = _idGenerator.NewIntegrationClientId(tenantDisplayName, applicationDisplayName, clientDisplayName),
            ClientSecretHash = _clientSecretHasher.Hash(clientSecret),
            AllowedScopes = normalizedScopes,
            CreatedUtc = now,
        };

        var directory = await _store.QuickCreateAsync(draft, cancellationToken);
        if (directory is null)
        {
            return AdminQuickCreateTenantResult.Failure(
                AdminQuickCreateTenantErrorCode.Conflict,
                $"Tenant '{tenantDisplayName}' already exists.");
        }

        var client = directory.IntegrationClients.Single(item => string.Equals(item.ClientId, draft.ClientId, StringComparison.Ordinal));
        await _auditWriter.WriteQuickCreatedAsync(adminContext, directory, client, cancellationToken);
        return AdminQuickCreateTenantResult.Success(directory, client, clientSecret);
    }
}
