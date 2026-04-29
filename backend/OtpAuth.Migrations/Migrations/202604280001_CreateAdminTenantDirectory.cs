using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604280001)]
public sealed class CreateAdminTenantDirectory : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.tenants (
                tenant_id uuid primary key,
                display_name varchar(200) not null,
                normalized_display_name varchar(200) not null,
                slug varchar(120) null,
                status varchar(32) not null default 'active',
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now()),
                constraint ck_tenants_status
                    check (status in ('active', 'disabled', 'archived', 'test')),
                constraint uq_tenants_normalized_display_name
                    unique (normalized_display_name),
                constraint uq_tenants_slug
                    unique (slug)
            );

            create table if not exists auth.tenant_applications (
                application_client_id uuid primary key,
                tenant_id uuid not null,
                display_name varchar(200) not null,
                normalized_display_name varchar(200) not null,
                slug varchar(120) null,
                status varchar(32) not null default 'active',
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now()),
                constraint fk_tenant_applications_tenant_id
                    foreign key (tenant_id)
                    references auth.tenants (tenant_id)
                    on delete restrict,
                constraint ck_tenant_applications_status
                    check (status in ('active', 'disabled', 'archived', 'test')),
                constraint uq_tenant_applications_tenant_name
                    unique (tenant_id, normalized_display_name),
                constraint uq_tenant_applications_tenant_slug
                    unique (tenant_id, slug)
            );

            create index if not exists ix_tenant_applications_tenant_id
                on auth.tenant_applications (tenant_id);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.tenant_applications;
            drop table if exists auth.tenants;
            """);
    }
}
