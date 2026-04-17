namespace OtpAuth.Api.Challenges;

public sealed record CreateChallengeHttpRequest
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required SubjectRefHttpRequest Subject { get; init; }

    public required OperationRefHttpRequest Operation { get; init; }

    public IReadOnlyCollection<string>? PreferredFactors { get; init; }

    public Guid? TargetDeviceId { get; init; }

    public CallbackRegistrationHttpRequest? Callback { get; init; }

    public string? CorrelationId { get; init; }
}

public sealed record SubjectRefHttpRequest
{
    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }
}

public sealed record OperationRefHttpRequest
{
    public required string Type { get; init; }

    public string? DisplayName { get; init; }
}

public sealed record CallbackRegistrationHttpRequest
{
    public required string Url { get; init; }
}
