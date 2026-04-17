# ADR-024: Unified Security Audit Trail Uses Sanitized Append-Only Events

## Status

Accepted

## Context

К `2026-04-15` в проекте уже существовали отдельные append-only audit trail для:

- signing key lifecycle
- `TOTP` protection key lifecycle

Параллельно operational lifecycle integration clients уже умел:

- rotate secret
- deactivate/reactivate client
- инвалидировать уже выданные JWT через `last_auth_state_changed_utc`

Но audit contour оставался фрагментированным:

1. reporting по security operations был разбит по разным таблицам и store-реализациям;
2. integration client lifecycle не попадал в тот же append-only trail;
3. дальнейшее расширение на admin/security operations грозило дублированием storage и operational команд.

## Decision

- вводится единая append-only таблица `auth.security_audit_events`
- новый generic слой `OtpAuth.Infrastructure.Security/*` становится каноническим storage/service для security audit events
- lifecycle snapshot-ы signing keys и `TOTP` protection keys продолжают сериализоваться только как sanitized metadata, но теперь пишутся через общий `SecurityAuditService`
- integration client lifecycle (`rotate`, `activate`, `deactivate`) тоже пишет sanitized events в тот же trail
- audit payload не должен содержать:
  - signing keys
  - `TOTP` protection keys
  - ciphertext
  - `client_secret`
  - `client_secret_hash`
- миграция backfill-ит существующие signing/`TOTP` lifecycle snapshot-ы в unified table
- migration runner получает generic reporting-команду `list-security-audit-events [limit] [event-type-prefix]`

## Consequences

- security-critical operational actions попадают в один append-only расследуемый trail
- reporting для signing/`TOTP`/integration lifecycle унифицируется без раскрытия secret material
- дальнейшее расширение audit contour на token issuance, enrollment lifecycle и admin operations можно делать без новых специализированных audit tables
- старые специализированные lifecycle tables остаются только как legacy/backfill-источник и не должны быть точкой развития
