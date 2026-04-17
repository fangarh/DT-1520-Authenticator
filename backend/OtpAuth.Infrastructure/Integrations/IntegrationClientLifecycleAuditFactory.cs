using System.Text.Json;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class IntegrationClientLifecycleAuditFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public SecurityAuditEntry CreateSecretRotatedEntry(
        string clientId,
        DateTimeOffset rotatedAtUtc,
        bool explicitSecretProvided)
    {
        return new SecurityAuditEntry
        {
            EventType = "integration_client_lifecycle.secret_rotated",
            SubjectType = "integration_client",
            SubjectId = clientId,
            Summary = $"client_id={clientId}; operation=secret_rotated; explicit_secret={explicitSecretProvided.ToString().ToLowerInvariant()}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                clientId,
                operation = "secret_rotated",
                explicitSecretProvided,
                rotatedAtUtc,
            }, SerializerOptions),
        };
    }

    public SecurityAuditEntry CreateStateChangedEntry(
        string clientId,
        bool isActive,
        bool stateChanged,
        DateTimeOffset changedAtUtc)
    {
        var state = isActive ? "activated" : "deactivated";
        var outcome = stateChanged ? "applied" : "already_applied";

        return new SecurityAuditEntry
        {
            EventType = stateChanged
                ? $"integration_client_lifecycle.{state}"
                : "integration_client_lifecycle.state_change_skipped",
            SubjectType = "integration_client",
            SubjectId = clientId,
            Summary = $"client_id={clientId}; operation={state}; outcome={outcome}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                clientId,
                operation = state,
                outcome,
                isActive,
                changedAtUtc,
            }, SerializerOptions),
            Severity = stateChanged ? "info" : "warning",
        };
    }
}
