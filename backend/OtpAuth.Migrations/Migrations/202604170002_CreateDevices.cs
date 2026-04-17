using FluentMigrator;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604170002)]
public sealed class CreateDevices : Migration
{
    public override void Up()
    {
        Execute.Sql(
            $"""
            create table if not exists auth.devices (
                id uuid primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                external_user_id varchar(256) not null,
                platform integer not null,
                installation_id varchar(128) not null,
                device_name varchar(128) null,
                status integer not null,
                attestation_status integer not null,
                push_token varchar(2048) null,
                public_key varchar(4096) null,
                activated_utc timestamptz null,
                last_seen_utc timestamptz null,
                last_auth_state_changed_utc timestamptz not null,
                revoked_utc timestamptz null,
                blocked_utc timestamptz null,
                created_utc timestamptz not null
            );

            create unique index if not exists ux_devices_active_installation
                on auth.devices (tenant_id, application_client_id, installation_id)
                where status = {(int)DeviceStatus.Active};

            create index if not exists ix_devices_tenant_application_external_user
                on auth.devices (tenant_id, application_client_id, external_user_id);

            create table if not exists auth.device_refresh_tokens (
                id uuid primary key,
                device_id uuid not null references auth.devices(id) on delete cascade,
                token_family_id uuid not null,
                token_hash varchar(512) not null,
                issued_utc timestamptz not null,
                expires_utc timestamptz not null,
                consumed_utc timestamptz null,
                revoked_utc timestamptz null,
                replaced_by_token_id uuid null,
                created_utc timestamptz not null
            );

            create index if not exists ix_device_refresh_tokens_device_id
                on auth.device_refresh_tokens (device_id);

            create index if not exists ix_device_refresh_tokens_family_id
                on auth.device_refresh_tokens (token_family_id);

            create table if not exists auth.device_activation_codes (
                id uuid primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                external_user_id varchar(256) not null,
                platform integer not null,
                code_hash varchar(512) not null,
                expires_utc timestamptz not null,
                consumed_utc timestamptz null,
                created_utc timestamptz not null
            );

            create index if not exists ix_device_activation_codes_active_lookup
                on auth.device_activation_codes (tenant_id, application_client_id, external_user_id, platform, expires_utc desc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.device_activation_codes;
            drop table if exists auth.device_refresh_tokens;
            drop table if exists auth.devices;
            """);
    }
}
