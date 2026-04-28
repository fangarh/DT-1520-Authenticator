using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604270001)]
public sealed class AddDeviceActivationCodeRevokedAt : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            alter table auth.device_activation_codes
                add column if not exists revoked_utc timestamptz null;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            alter table auth.device_activation_codes
                drop column if exists revoked_utc;
            """);
    }
}
