namespace Dt1520.Authenticator.Client;

/// <summary>
/// Sanitized SDK error returned for expected DT-1520 failures.
/// </summary>
public sealed record Dt1520AuthenticatorError
{
    /// <summary>
    /// Creates a sanitized SDK error.
    /// </summary>
    public Dt1520AuthenticatorError(
        Dt1520AuthenticatorErrorKind kind,
        int? statusCode,
        string title,
        string? detail = null,
        string? type = null,
        string? traceId = null,
        string? requestId = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Kind = kind;
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Type = type;
        TraceId = traceId;
        RequestId = requestId;
        ValidationErrors = validationErrors;
    }

    /// <summary>
    /// Stable SDK error category.
    /// </summary>
    public Dt1520AuthenticatorErrorKind Kind { get; }

    /// <summary>
    /// HTTP status code when the error came from DT-1520.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Safe error title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Safe error detail when DT-1520 provided one.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Problem type URI when DT-1520 provided one.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Safe trace identifier when DT-1520 provided one.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Safe request identifier when DT-1520 provided one.
    /// </summary>
    public string? RequestId { get; }

    /// <summary>
    /// Field-level validation errors when DT-1520 returned them.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(Dt1520AuthenticatorError)} {{ {nameof(Kind)} = {Kind}, {nameof(StatusCode)} = {StatusCode}, {nameof(Title)} = {Title}, {nameof(RequestId)} = {RequestId} }}";
    }
}
