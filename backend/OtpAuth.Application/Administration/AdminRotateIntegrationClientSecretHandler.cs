using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Administration;

public sealed class AdminRotateIntegrationClientSecretHandler
{
    private readonly IAdminIntegrationClientStore _store;
    private readonly IClientSecretHasher _clientSecretHasher;
    private readonly IAdminIntegrationClientSecretGenerator _secretGenerator;
    private readonly IAdminIntegrationClientAuditWriter _auditWriter;

    public AdminRotateIntegrationClientSecretHandler(
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

    public async Task<AdminRotateIntegrationClientSecretResult> HandleAsync(
        AdminIntegrationClientRouteRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.IntegrationClientsWrite))
        {
            return AdminRotateIntegrationClientSecretResult.Failure(
                AdminRotateIntegrationClientSecretErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.IntegrationClientsWrite}' is required.");
        }

        var validationError = ValidateRoute(request, out var normalizedClientId);
        if (validationError is not null)
        {
            return AdminRotateIntegrationClientSecretResult.Failure(
                AdminRotateIntegrationClientSecretErrorCode.ValidationFailed,
                validationError);
        }

        var clientSecret = _secretGenerator.Generate();
        var changedAtUtc = DateTimeOffset.UtcNow;
        var client = await _store.RotateSecretAsync(
            request.TenantId,
            normalizedClientId!,
            _clientSecretHasher.Hash(clientSecret),
            changedAtUtc,
            cancellationToken);
        if (client is null)
        {
            return AdminRotateIntegrationClientSecretResult.Failure(
                AdminRotateIntegrationClientSecretErrorCode.NotFound,
                "Integration client was not found.");
        }

        await _auditWriter.WriteSecretRotatedAsync(adminContext, client, cancellationToken);
        return AdminRotateIntegrationClientSecretResult.Success(client, clientSecret);
    }

    private static string? ValidateRoute(
        AdminIntegrationClientRouteRequest request,
        out string? normalizedClientId)
    {
        normalizedClientId = AdminIntegrationClientValidation.NormalizeClientId(request.ClientId);
        if (normalizedClientId is null)
        {
            return "ClientId must be 1-200 characters and contain only letters, digits, '.', '_' or '-'.";
        }

        return request.TenantId == Guid.Empty
            ? "TenantId is required."
            : null;
    }
}
