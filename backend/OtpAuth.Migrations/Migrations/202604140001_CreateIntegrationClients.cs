using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140001)]
public sealed class CreateIntegrationClients : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create schema if not exists auth;

            create table if not exists auth.integration_clients (
                client_id varchar(200) primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                client_secret_hash varchar(1024) not null,
                is_active boolean not null default true,
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now()),
                last_secret_rotated_utc timestamptz null
            );

            create table if not exists auth.integration_client_scopes (
                client_id varchar(200) not null,
                scope varchar(200) not null,
                primary key (client_id, scope),
                constraint fk_integration_client_scopes_client_id
                    foreign key (client_id)
                    references auth.integration_clients (client_id)
                    on delete cascade
            );

            create index if not exists ix_integration_clients_tenant_application
                on auth.integration_clients (tenant_id, application_client_id);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.integration_client_scopes;
            drop table if exists auth.integration_clients;
            """);
    }
}
