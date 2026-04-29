namespace OtpAuth.Application.Administration;

public sealed class AdminCreateTenantHandler
{
    private readonly IAdminTenantDirectoryStore _store;
    private readonly IAdminTenantDirectoryIdGenerator _idGenerator;
    private readonly IAdminTenantDirectoryAuditWriter _auditWriter;

    public AdminCreateTenantHandler(
        IAdminTenantDirectoryStore store,
        IAdminTenantDirectoryIdGenerator idGenerator,
        IAdminTenantDirectoryAuditWriter auditWriter)
    {
        _store = store;
        _idGenerator = idGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<AdminCreateTenantResult> HandleAsync(
        AdminTenantCreateRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.TenantsWrite))
        {
            return AdminCreateTenantResult.Failure(
                AdminCreateTenantErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.TenantsWrite}' is required.");
        }

        var displayName = AdminTenantDirectoryValidation.NormalizeDisplayName(request.DisplayName);
        if (displayName is null)
        {
            return AdminCreateTenantResult.Failure(
                AdminCreateTenantErrorCode.ValidationFailed,
                "Tenant display name must be 1-200 characters.");
        }

        var tenantId = request.TenantId.GetValueOrDefault(_idGenerator.NewTenantId());
        if (tenantId == Guid.Empty)
        {
            return AdminCreateTenantResult.Failure(
                AdminCreateTenantErrorCode.ValidationFailed,
                "TenantId cannot be empty.");
        }

        var slug = request.Slug is null
            ? AdminTenantDirectoryValidation.CreateSlugCandidate(displayName, "tenant")
            : AdminTenantDirectoryValidation.NormalizeSlug(request.Slug);
        if (slug is null)
        {
            return AdminCreateTenantResult.Failure(
                AdminCreateTenantErrorCode.ValidationFailed,
                "Tenant slug must be 120 characters or fewer and contain only letters, digits, '-' or '_'.");
        }

        var tenant = await _store.CreateTenantAsync(
            new AdminTenantCreateDraft
            {
                TenantId = tenantId,
                DisplayName = displayName,
                Slug = slug,
                Status = request.Status,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            cancellationToken);
        if (tenant is null)
        {
            return AdminCreateTenantResult.Failure(
                AdminCreateTenantErrorCode.Conflict,
                $"Tenant '{displayName}' already exists.");
        }

        await _auditWriter.WriteTenantCreatedAsync(adminContext, tenant, cancellationToken);
        return AdminCreateTenantResult.Success(tenant);
    }
}
