using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604200003)]
public sealed class AddAdminDeviceLookupIndex : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create index if not exists ix_devices_tenant_external_user_status
                on auth.devices (tenant_id, external_user_id, status, created_utc desc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop index if exists auth.ix_devices_tenant_external_user_status;
            """);
    }
}
