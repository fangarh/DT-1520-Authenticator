using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140006)]
public sealed class AddIntegrationClientAuthStateTracking : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            alter table auth.integration_clients
                add column if not exists last_auth_state_changed_utc timestamptz;

            update auth.integration_clients
            set last_auth_state_changed_utc = coalesce(
                last_auth_state_changed_utc,
                last_secret_rotated_utc,
                updated_utc,
                created_utc,
                timezone('utc', now()))
            where last_auth_state_changed_utc is null;

            alter table auth.integration_clients
                alter column last_auth_state_changed_utc set not null;

            alter table auth.integration_clients
                alter column last_auth_state_changed_utc set default timezone('utc', now());
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            alter table auth.integration_clients
                drop column if exists last_auth_state_changed_utc;
            """);
    }
}
