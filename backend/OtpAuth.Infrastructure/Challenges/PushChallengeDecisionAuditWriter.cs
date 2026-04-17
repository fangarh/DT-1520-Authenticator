using System.Text.Json;
using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class PushChallengeDecisionAuditWriter : IChallengeDecisionAuditWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SecurityAuditService _securityAuditService;

    public PushChallengeDecisionAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteApprovedAsync(
        Challenge challenge,
        RegisteredDevice device,
        bool biometricVerified,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "challenge.approved",
                challenge,
                $"challenge_id={challenge.Id}; decision=approved; device_id={device.Id}",
                new
                {
                    challengeId = challenge.Id,
                    tenantId = challenge.TenantId,
                    applicationClientId = challenge.ApplicationClientId,
                    deviceId = device.Id,
                    factorType = challenge.FactorType.ToString().ToLowerInvariant(),
                    status = challenge.Status.ToString().ToLowerInvariant(),
                    approvedAtUtc = challenge.ApprovedUtc,
                    biometricVerified,
                }),
            cancellationToken);
    }

    public Task WriteDeniedAsync(
        Challenge challenge,
        RegisteredDevice device,
        bool hasReason,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "challenge.denied",
                challenge,
                $"challenge_id={challenge.Id}; decision=denied; device_id={device.Id}",
                new
                {
                    challengeId = challenge.Id,
                    tenantId = challenge.TenantId,
                    applicationClientId = challenge.ApplicationClientId,
                    deviceId = device.Id,
                    factorType = challenge.FactorType.ToString().ToLowerInvariant(),
                    status = challenge.Status.ToString().ToLowerInvariant(),
                    deniedAtUtc = challenge.DeniedUtc,
                    hasReason,
                }),
            cancellationToken);
    }

    private static SecurityAuditEntry CreateEntry(
        string eventType,
        Challenge challenge,
        string summary,
        object payload)
    {
        return new SecurityAuditEntry
        {
            EventType = eventType,
            SubjectType = "challenge",
            SubjectId = challenge.Id.ToString(),
            Summary = summary,
            PayloadJson = JsonSerializer.Serialize(payload, SerializerOptions),
            Severity = "info",
            Source = "push_challenge",
        };
    }
}
