using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604170001)]
public sealed class CreateBackupCodes : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.backup_codes (
                id uuid primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                external_user_id varchar(256) not null,
                username varchar(256) null,
                code_hash varchar(512) not null,
                created_utc timestamptz not null default timezone('utc', now()),
                used_utc timestamptz null
            );

            create index if not exists ix_backup_codes_scope_active
                on auth.backup_codes (tenant_id, application_client_id, external_user_id)
                where used_utc is null;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.backup_codes;
            """);
    }
}
