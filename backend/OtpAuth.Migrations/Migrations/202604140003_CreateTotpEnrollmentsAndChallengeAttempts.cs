using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140003)]
public sealed class CreateTotpEnrollmentsAndChallengeAttempts : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create extension if not exists pgcrypto;

            create table if not exists auth.totp_enrollments (
                id uuid primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                external_user_id varchar(256) not null,
                username varchar(256) null,
                secret_ciphertext bytea not null,
                secret_nonce bytea not null,
                secret_tag bytea not null,
                key_version integer not null,
                digits integer not null,
                period_seconds integer not null,
                algorithm varchar(32) not null,
                is_active boolean not null default true,
                confirmed_utc timestamptz null,
                last_used_utc timestamptz null,
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now()),
                constraint uq_totp_enrollments_scope unique (tenant_id, application_client_id, external_user_id)
            );

            create index if not exists ix_totp_enrollments_scope_active
                on auth.totp_enrollments (tenant_id, application_client_id, external_user_id, is_active);

            create table if not exists auth.challenge_attempts (
                id uuid primary key,
                challenge_id uuid not null,
                attempt_type varchar(64) not null,
                result varchar(64) not null,
                created_utc timestamptz not null,
                constraint fk_challenge_attempts_challenge_id
                    foreign key (challenge_id)
                    references auth.challenges (id)
                    on delete cascade
            );

            create index if not exists ix_challenge_attempts_challenge_id_created_utc
                on auth.challenge_attempts (challenge_id, created_utc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.challenge_attempts;
            drop table if exists auth.totp_enrollments;
            """);
    }
}
