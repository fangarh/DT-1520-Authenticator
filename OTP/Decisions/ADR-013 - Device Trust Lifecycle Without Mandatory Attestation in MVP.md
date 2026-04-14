# ADR-013: Device Trust Lifecycle Without Mandatory Attestation in MVP

## Status

Accepted

## Context

Для `push approval` нужно явно определить, когда устройство считается доверенным и какие условия обязательны для approve flow. Полноценная обязательная attestation в `v1` повышает надежность, но заметно усложняет delivery, on-prem сценарии и первую Android-реализацию.

При этом отсутствие trust lifecycle вообще недопустимо для security-sensitive фактора.

## Decision

- в `MVP` вводится обязательный server-side lifecycle доверия устройства: `pending`, `active`, `revoked`, `blocked`
- `push approval` разрешен только для устройства в статусе `active`
- device attestation в `v1` не является обязательным blocking requirement
- поле `attestation_status` сохраняется в модели как сигнал и точка расширения
- revoke устройства должен немедленно блокировать device token refresh и approve flow

## Consequences

- `MVP` получает управляемую и аудируемую trust model без перегрузки первой версии
- сервер обязан проверять trust state на каждом security-critical device flow
- mobile и backend должны поддерживать полноценный revoke semantics, а не только UI-скрытие устройства
- обязательная attestation может быть введена позже отдельным решением без слома базового lifecycle
