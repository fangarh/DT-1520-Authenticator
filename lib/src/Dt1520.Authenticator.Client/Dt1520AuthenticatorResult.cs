namespace Dt1520.Authenticator.Client;

/// <summary>
/// Result wrapper used by DT-1520 SDK operations for expected remote outcomes.
/// </summary>
/// <typeparam name="T">Successful value type.</typeparam>
public sealed class Dt1520AuthenticatorResult<T>
{
    private Dt1520AuthenticatorResult(T? value, Dt1520AuthenticatorError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Indicates whether the SDK operation completed successfully.
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Successful value. This is <c>null</c> when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Sanitized SDK error. This is <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public Dt1520AuthenticatorError? Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Dt1520AuthenticatorResult<T> Success(T value)
    {
        return new Dt1520AuthenticatorResult<T>(value, null);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Dt1520AuthenticatorResult<T> Failure(Dt1520AuthenticatorError error)
    {
        return new Dt1520AuthenticatorResult<T>(default, error);
    }
}
