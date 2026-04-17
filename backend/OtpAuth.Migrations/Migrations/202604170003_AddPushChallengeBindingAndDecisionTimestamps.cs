using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604170003)]
public sealed class AddPushChallengeBindingAndDecisionTimestamps : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            alter table auth.challenges
                add column if not exists target_device_id uuid null,
                add column if not exists approved_utc timestamptz null,
                add column if not exists denied_utc timestamptz null;

            create index if not exists ix_challenges_target_device_id
                on auth.challenges (target_device_id);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop index if exists auth.ix_challenges_target_device_id;

            alter table auth.challenges
                drop column if exists denied_utc,
                drop column if exists approved_utc,
                drop column if exists target_device_id;
            """);
    }
}
