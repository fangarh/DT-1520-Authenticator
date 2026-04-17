namespace OtpAuth.Worker;

public sealed record RedisConnectionTarget(
    string Host,
    int Port)
{
    public static bool TryParse(string connectionString, out RedisConnectionTarget? target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var endpointToken = connectionString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(part => !part.Contains('='));

        if (string.IsNullOrWhiteSpace(endpointToken))
        {
            return false;
        }

        var separatorIndex = endpointToken.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= endpointToken.Length - 1)
        {
            return false;
        }

        var host = endpointToken[..separatorIndex];
        if (!int.TryParse(endpointToken[(separatorIndex + 1)..], out var port) || port <= 0 || port > 65535)
        {
            return false;
        }

        target = new RedisConnectionTarget(host, port);
        return true;
    }
}
