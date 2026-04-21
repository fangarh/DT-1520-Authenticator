using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604200002)]
public sealed class CreateWebhookSubscriptionsAndDeliveries : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table auth.webhook_subscriptions (
                id uuid primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                endpoint_url text not null,
                status text not null,
                created_utc timestamptz not null,
                updated_utc timestamptz not null,
                constraint uq_webhook_subscriptions_endpoint unique (tenant_id, application_client_id, endpoint_url),
                constraint chk_webhook_subscriptions_status check (status in ('active', 'inactive'))
            );

            create index ix_webhook_subscriptions_scope
                on auth.webhook_subscriptions (tenant_id, application_client_id, status);

            create table auth.webhook_subscription_event_types (
                subscription_id uuid not null references auth.webhook_subscriptions (id) on delete cascade,
                event_type text not null,
                created_utc timestamptz not null default timezone('utc', now()),
                constraint pk_webhook_subscription_event_types primary key (subscription_id, event_type),
                constraint chk_webhook_subscription_event_type check (
                    event_type in (
                        'challenge.approved',
                        'challenge.denied',
                        'challenge.expired',
                        'device.activated',
                        'device.revoked',
                        'device.blocked',
                        'factor.revoked'
                    )
                )
            );

            create table auth.webhook_event_deliveries (
                id uuid primary key,
                subscription_id uuid not null references auth.webhook_subscriptions (id) on delete cascade,
                tenant_id uuid not null,
                application_client_id uuid not null,
                endpoint_url text not null,
                event_id uuid not null,
                event_type text not null,
                occurred_utc timestamptz not null,
                resource_type text not null,
                resource_id uuid not null,
                payload_json jsonb not null,
                status text not null,
                attempt_count integer not null,
                next_attempt_utc timestamptz not null,
                last_attempt_utc timestamptz null,
                delivered_utc timestamptz null,
                last_error_code text null,
                locked_until_utc timestamptz null,
                created_utc timestamptz not null,
                updated_utc timestamptz not null,
                constraint uq_webhook_event_deliveries_subscription_event unique (subscription_id, event_id),
                constraint chk_webhook_event_deliveries_status check (status in ('queued', 'delivered', 'failed')),
                constraint chk_webhook_event_deliveries_event_type check (
                    event_type in (
                        'challenge.approved',
                        'challenge.denied',
                        'challenge.expired',
                        'device.activated',
                        'device.revoked',
                        'device.blocked',
                        'factor.revoked'
                    )
                ),
                constraint chk_webhook_event_deliveries_resource_type check (resource_type in ('challenge', 'device', 'factor'))
            );

            create index ix_webhook_event_deliveries_due
                on auth.webhook_event_deliveries (status, next_attempt_utc, locked_until_utc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.webhook_event_deliveries;
            drop table if exists auth.webhook_subscription_event_types;
            drop table if exists auth.webhook_subscriptions;
            """);
    }
}
