# TOTP Enrollment Management Iterations

## Status

Completed

## Цель

Закрыть backend lifecycle для operator-facing `TOTP` enrollment так, чтобы после этого можно было безопасно начинать `admin UI` и `Android TOTP-first` без опоры на временные API.

## Iteration 1. Enrollment status/read

### Scope

- `GET /api/v1/enrollments/totp/{enrollmentId}`
- application read handler для enrollment status
- transport/error contract для `404/403`
- unit tests

### Goal

Дать `admin UI` и operator flows стабильный read path для enrollment, на который уже сейчас ссылается `Location` после start endpoint.

### Notes

- endpoint не должен возвращать `secretUri`, `qrCodePayload` и другие provisioning artifacts после start
- доступ остается scope-bound и tenant/application scoped

### Status

Completed

## Iteration 2. Enrollment revoke

### Scope

- revoke endpoint
- deactivation persistence path
- audit event
- unit tests и security checks

### Goal

Закрыть обязательный destructive operator flow без UI-only revoke.

### Status

Completed

## Iteration 3. Enrollment replace

### Scope

- безопасный replace flow поверх existing enrollment
- явная модель replace semantics: old enrollment не теряется до успешного перехода
- audit и conflict handling

### Goal

Дать оператору безопасную замену фактора без скрытого разрушения активного доступа.

### Status

Completed

## Iteration 4. Endpoint hardening

### Scope

- endpoint/integration tests для enrollment management API
- проверка `401/403/404/409`
- contract alignment с `openapi-v1.yaml`

### Goal

Подготовить backend к началу `admin UI` без постоянного передела transport-контрактов.

### Status

Completed

## Exit criteria

Backend считается готовым к старту `admin UI`, когда:

- реализованы `start / confirm / status / revoke / replace`
- enrollment management покрыт unit и endpoint-level tests
- response contracts стабильны и не раскрывают provisioning secrets
- audit покрывает lifecycle management actions

Эти критерии на backend-стороне выполнены по состоянию на `2026-04-15`; дальнейший риск теперь смещается в `admin UI` implementation quality и последующие factor/device slices.

## Последнее обновление

- `2026-04-15`: завершена Iteration 1 `Enrollment status/read` через `GET /api/v1/enrollments/totp/{enrollmentId}`; read path не возвращает provisioning artifacts и проходит backend verification
- `2026-04-15`: завершена Iteration 2 `Enrollment revoke` через `POST /api/v1/enrollments/totp/{enrollmentId}/revoke`; revoke переводит enrollment в `revoked`, пишет sanitized audit event и проходит backend verification
- `2026-04-15`: завершена Iteration 3 `Enrollment replace` через `POST /api/v1/enrollments/totp/{enrollmentId}/replace`; current factor остается активным до успешного confirm replacement, а replacement confirm-path использует отдельный persisted attempt counter
- `2026-04-15`: завершена Iteration 4 `Endpoint hardening`; добавлен in-memory API test harness поверх `WebApplicationFactory`, покрывающий `401/403/409`, safe replacement confirm-path, `Location` header и отсутствие provisioning artifacts в read/revoke/confirm responses
