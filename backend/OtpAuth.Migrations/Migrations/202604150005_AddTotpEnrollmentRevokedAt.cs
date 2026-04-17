using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604150005)]
public sealed class AddTotpEnrollmentRevokedAt : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            alter table auth.totp_enrollments
                add column if not exists revoked_utc timestamptz null;

            update auth.totp_enrollments
            set revoked_utc = updated_utc
            where is_active = false
              and revoked_utc is null;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            alter table auth.totp_enrollments
                drop column if exists revoked_utc;
            """);
    }
}
