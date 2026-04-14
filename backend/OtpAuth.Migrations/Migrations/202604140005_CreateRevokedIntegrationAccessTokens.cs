using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140005)]
public sealed class CreateRevokedIntegrationAccessTokens : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.revoked_integration_access_tokens (
                jwt_id varchar(64) primary key,
                client_id varchar(200) not null,
                revoked_utc timestamptz not null,
                expires_utc timestamptz not null,
                reason varchar(128) null
            );

            create index if not exists ix_revoked_integration_access_tokens_expires_utc
                on auth.revoked_integration_access_tokens (expires_utc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.revoked_integration_access_tokens;
            """);
    }
}
