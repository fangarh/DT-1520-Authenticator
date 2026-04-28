namespace Dt1520.Authenticator.ReferenceBackend;

public sealed record ReferenceBackendResult<T>
{
    public bool IsSuccess { get; init; }

    public T? Value { get; init; }

    public ReferenceBackendError? Error { get; init; }

    public static ReferenceBackendResult<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value,
    };

    public static ReferenceBackendResult<T> Failure(
        string title,
        int statusCode,
        string? detail = null) => new()
        {
            Error = new ReferenceBackendError(title, statusCode, detail),
        };
}

public sealed record ReferenceBackendError(string Title, int StatusCode, string? Detail);

public static class ReferenceBackendResultExtensions
{
    public static IResult ToHttpResult<T>(this ReferenceBackendResult<T> result)
    {
        var error = result.Error ?? new ReferenceBackendError(
            "Reference backend request failed.",
            StatusCodes.Status500InternalServerError,
            null);

        return Results.Problem(
            title: error.Title,
            detail: error.Detail,
            statusCode: error.StatusCode);
    }
}
