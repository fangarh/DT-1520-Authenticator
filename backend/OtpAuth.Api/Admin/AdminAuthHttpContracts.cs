namespace OtpAuth.Api.Admin;

public sealed record AdminLoginHttpRequest
{
    public required string Username { get; init; }

    public required string Password { get; init; }
}

public sealed record AdminSessionHttpResponse
{
    public required Guid AdminUserId { get; init; }

    public required string Username { get; init; }

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
}

public sealed record AdminCsrfTokenHttpResponse
{
    public required string RequestToken { get; init; }
}
