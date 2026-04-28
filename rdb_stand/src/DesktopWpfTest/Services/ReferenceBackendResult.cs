namespace Dt1520.Authenticator.DesktopWpfTest.Services;

public sealed record ReferenceBackendResult<T>
{
    private ReferenceBackendResult(T? value, bool isSuccess, string? errorMessage, int? statusCode)
    {
        Value = value;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public T? Value { get; }

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    public int? StatusCode { get; }

    public static ReferenceBackendResult<T> Success(T value)
    {
        return new ReferenceBackendResult<T>(value, true, null, null);
    }

    public static ReferenceBackendResult<T> Failure(string errorMessage, int? statusCode = null)
    {
        return new ReferenceBackendResult<T>(default, false, errorMessage, statusCode);
    }
}
