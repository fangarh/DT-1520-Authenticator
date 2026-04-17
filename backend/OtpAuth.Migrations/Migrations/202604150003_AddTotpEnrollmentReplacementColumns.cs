using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604150003)]
public sealed class AddTotpEnrollmentReplacementColumns : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            alter table auth.totp_enrollments
                add column if not exists replacement_secret_ciphertext bytea null,
                add column if not exists replacement_secret_nonce bytea null,
                add column if not exists replacement_secret_tag bytea null,
                add column if not exists replacement_key_version integer null,
                add column if not exists replacement_digits integer null,
                add column if not exists replacement_period_seconds integer null,
                add column if not exists replacement_algorithm varchar(32) null,
                add column if not exists replacement_started_utc timestamptz null,
                add column if not exists replacement_failed_confirm_attempts integer not null default 0;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            alter table auth.totp_enrollments
                drop column if exists replacement_failed_confirm_attempts,
                drop column if exists replacement_started_utc,
                drop column if exists replacement_algorithm,
                drop column if exists replacement_period_seconds,
                drop column if exists replacement_digits,
                drop column if exists replacement_key_version,
                drop column if exists replacement_secret_tag,
                drop column if exists replacement_secret_nonce,
                drop column if exists replacement_secret_ciphertext;
            """);
    }
}
