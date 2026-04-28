using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Administration;

public sealed class AdminCreateIntegrationClientHandler
{
    private readonly IAdminIntegrationClientStore _store;
    private readonly IClientSecretHasher _clientSecretHasher;
    private readonly IAdminIntegrationClientSecretGenerator _secretGenerator;
    private readonly IAdminIntegrationClientAuditWriter _auditWriter;

    public AdminCreateIntegrationClientHandler(
        IAdminIntegrationClientStore store,
        IClientSecretHasher clientSecretHasher,
        IAdminIntegrationClientSecretGenerator secretGenerator,
        IAdminIntegrationClientAuditWriter auditWriter)
    {
        _store = store;
        _clientSecretHasher = clientSecretHasher;
        _secretGenerator = secretGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<AdminCreateIntegrationClientResult> HandleAsync(
        AdminIntegrationClientCreateRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.IntegrationClientsWrite))
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.IntegrationClientsWrite}' is required.");
        }

        var normalizedClientId = AdminIntegrationClientValidation.NormalizeClientId(request.ClientId);
        if (normalizedClientId is null)
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.ValidationFailed,
                "ClientId must be 1-200 characters and contain only letters, digits, '.', '_' or '-'.");
        }

        if (request.TenantId == Guid.Empty)
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.ValidationFailed,
                "ApplicationClientId is required.");
        }

        if (request.HasOperatorProvidedSecret)
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.ValidationFailed,
                "Admin client creation generates client secrets server-side.");
        }

        var normalizedScopes = AdminIntegrationClientValidation.NormalizeScopes(request.AllowedScopes, out var scopeError);
        if (scopeError is not null)
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.ValidationFailed,
                scopeError);
        }

        var clientSecret = _secretGenerator.Generate();
        var now = DateTimeOffset.UtcNow;
        var createdClient = await _store.CreateAsync(
            new AdminIntegrationClientCreateDraft
            {
                ClientId = normalizedClientId,
                TenantId = request.TenantId,
                ApplicationClientId = request.ApplicationClientId,
                ClientSecretHash = _clientSecretHasher.Hash(clientSecret),
                AllowedScopes = normalizedScopes,
                CreatedUtc = now,
            },
            cancellationToken);
        if (createdClient is null)
        {
            return AdminCreateIntegrationClientResult.Failure(
                AdminCreateIntegrationClientErrorCode.Conflict,
                $"Integration client '{normalizedClientId}' already exists.");
        }

        await _auditWriter.WriteCreatedAsync(adminContext, createdClient, cancellationToken);
        return AdminCreateIntegrationClientResult.Success(createdClient, clientSecret);
    }
}
