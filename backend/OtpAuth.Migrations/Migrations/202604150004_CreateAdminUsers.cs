using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604150004)]
public sealed class CreateAdminUsers : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.admin_users (
                id uuid primary key,
                username varchar(200) not null,
                normalized_username varchar(200) not null,
                password_hash varchar(1024) not null,
                is_active boolean not null default true,
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now()),
                last_login_utc timestamptz null,
                constraint uq_admin_users_normalized_username unique (normalized_username)
            );

            create table if not exists auth.admin_user_permissions (
                admin_user_id uuid not null,
                permission varchar(200) not null,
                primary key (admin_user_id, permission),
                constraint fk_admin_user_permissions_admin_user_id
                    foreign key (admin_user_id)
                    references auth.admin_users (id)
                    on delete cascade
            );

            create index if not exists ix_admin_users_is_active
                on auth.admin_users (is_active);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.admin_user_permissions;
            drop table if exists auth.admin_users;
            """);
    }
}
