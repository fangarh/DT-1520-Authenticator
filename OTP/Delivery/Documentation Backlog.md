# Documentation Backlog

## Цель

Собрать недостающие документы так, чтобы команда могла переходить от архитектурного каркаса к реализации без постоянных допущений.

## `P0`

- [[../Architecture/Backend Module Design]]
- [[../Security/Security Model MVP]]
- [[Testing Strategy]]

## Уже закрыто

- `ADR-011 - MVP Tenancy Model`
- `ADR-012 - Admin-Led Enrollment for MVP`
- `ADR-013 - Device Trust Lifecycle Without Mandatory Attestation in MVP`
- `ADR-014 - 2FA Server as MVP and IdP as Strategic Target`
- `ADR-015 - Push Optional for On-Prem and Future Air-Gapped Profiles`

## `P1`

- `OTP/Architecture/Backend Project Layout.md`
- `OTP/Data/Persistence and Migrations Plan.md`
- `OTP/Integrations/API Conventions.md`
- `OTP/Product/Admin Flows MVP.md`
- `OTP/Product/Mobile User Flows MVP.md`

## `P2`

- `OTP/Delivery/Local Development Runbook.md`
- `OTP/Delivery/CI-CD and Release Flow.md`
- `OTP/Delivery/Observability and Alerting.md`
- `OTP/Integrations/SDK Strategy.md`
- `OTP/Delivery/On-Prem Operations Runbook.md`

## Критерий завершения `P0`

- нет открытых решений, которые ломают модель данных или auth flow
- backend-модули разложены по use cases и границам
- security controls зафиксированы до начала активной реализации
- test strategy определяет обязательные проверки для каждого слоя
