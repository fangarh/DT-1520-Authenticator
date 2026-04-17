using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604150002)]
public sealed class AddTotpEnrollmentConfirmationAttemptTracking : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            alter table auth.totp_enrollments
                add column if not exists failed_confirm_attempts integer not null default 0;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            alter table auth.totp_enrollments
                drop column if exists failed_confirm_attempts;
            """);
    }
}
