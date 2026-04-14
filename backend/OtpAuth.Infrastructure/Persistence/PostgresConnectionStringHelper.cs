using Npgsql;

namespace OtpAuth.Infrastructure.Persistence;

public static class PostgresConnectionStringHelper
{
    public static string GetRequiredDatabaseName(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("PostgreSQL connection string must include a database name.");
        }

        return builder.Database;
    }

    public static string BuildMaintenanceConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Database = "postgres";
        builder.Pooling = false;

        return builder.ConnectionString;
    }
}
