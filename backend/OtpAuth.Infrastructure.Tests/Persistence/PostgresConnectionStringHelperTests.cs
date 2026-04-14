using OtpAuth.Infrastructure.Persistence;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Persistence;

public sealed class PostgresConnectionStringHelperTests
{
    [Fact]
    public void GetRequiredDatabaseName_ReturnsDatabaseName()
    {
        const string connectionString =
            "Host=ghostring.ru;Port=5432;Database=dt-auth;Username=sa;Password=secret";

        var databaseName = PostgresConnectionStringHelper.GetRequiredDatabaseName(connectionString);

        Assert.Equal("dt-auth", databaseName);
    }

    [Fact]
    public void BuildMaintenanceConnectionString_SwitchesDatabaseToPostgres()
    {
        const string connectionString =
            "Host=ghostring.ru;Port=5432;Database=dt-auth;Username=sa;Password=secret;Pooling=true";

        var maintenanceConnectionString = PostgresConnectionStringHelper.BuildMaintenanceConnectionString(connectionString);

        Assert.Contains("Database=postgres", maintenanceConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pooling=False", maintenanceConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRequiredDatabaseName_ThrowsWhenDatabaseIsMissing()
    {
        const string connectionString =
            "Host=ghostring.ru;Port=5432;Username=sa;Password=secret";

        var error = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionStringHelper.GetRequiredDatabaseName(connectionString));

        Assert.Contains("database name", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
