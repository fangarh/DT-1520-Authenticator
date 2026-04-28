using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend.Tests;

internal sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = currentValue;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
