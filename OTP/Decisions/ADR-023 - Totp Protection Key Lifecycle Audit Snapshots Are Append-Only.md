# ADR-023: TOTP Protection Key Lifecycle Audit Snapshots Are Append-Only

## Status

Accepted

## Context

`TOTP` protection key rotation уже поддерживает:

- current + legacy keys по `key_version`
- отдельную maintenance-операцию `reencrypt-totp-secrets`

Но без audit/reporting остается operational blind spot:

1. нельзя доказать, какой `key_version` runtime считал current на момент проверки;
2. нельзя быстро увидеть backlog enrollment-ов, требующих re-encryption;
3. нельзя сохранить sanitized snapshot для расследования без доступа к key material.

## Decision

- для `TOTP` protection key lifecycle вводится append-only таблица `auth.totp_protection_key_audit_events`
- audit snapshot содержит только sanitized metadata:
  - current `key_version`
  - usage enrollment-ов по `key_version`
  - re-encryption backlog
  - warnings
  - summary
- signing material и ciphertext в audit payload не сохраняются
- inspection, audit и reporting выполняются отдельными operational командами migration runner-а
- для audit/reporting достаточно metadata `TotpProtection` config; actual key bytes не требуются

## Consequences

- protection key lifecycle становится расследуемым без хранения секретов в audit trail
- оператор может проверять readiness к retirement legacy key version до удаления legacy key из runtime config
- reporting может выполняться against `PostgreSQL` отдельно от maintenance re-encryption
- generic audit contour для остальных factor secrets и security flows остается будущей задачей
