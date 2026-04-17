# ADR-031 - Push Challenges Are Bound to a Single Active Device

## Status

Accepted

## Context

После `ADR-030` у backend уже появился отдельный `DeviceBearer` lifecycle для mobile devices, но сам `push approve/deny` contour все еще оставался потенциально неоднозначным.

Если `push` challenge не привязан server-side к конкретному device record, любой другой активный device того же пользователя в рамках того же `tenant/application client` сможет попытаться подтвердить challenge тем же `DeviceBearer`.

Это нарушает ожидаемую semantics `device-bound push approval`, ослабляет расследуемость и делает approve flow зависимым от локальной эвристики клиента вместо явного server-side binding.

## Decision

- `push` challenge в persistence хранит `target_device_id`.
- `POST /api/v1/challenges/{challengeId}/approve` и `POST /api/v1/challenges/{challengeId}/deny` разрешены только под `DeviceBearer` и только если:
  - challenge находится в `pending`;
  - challenge еще не expired;
  - `factor_type = push`;
  - `target_device_id` совпадает с authenticated `device_id`;
  - device принадлежит тому же `external_user_id`, что и challenge;
  - `Policy` разрешает `push` для текущего device trust context.
- approve flow требует `biometricVerified = true` как минимальный MVP signal локальной user presence; это не считается полноценной attestation и не заменяет будущие stronger proofs.
- при создании challenge backend auto-bind-ит `push` только если для `(tenantId, applicationClientId, externalUserId)` найден ровно один активный push-capable device; иначе `push` fail-closed не выбирается и policy уходит в безопасный fallback/deny.
- challenge lifecycle хранит `approved_utc` и `denied_utc`.
- approve/deny пишут sanitized append-only audit события `challenge.approved` и `challenge.denied`.

## Consequences

- `push approve/deny` становится реально device-bound, а не просто device-authenticated.
- runtime `CreateChallenge` получает безопасный минимальный путь к созданию `push` challenge без нового публичного transport-поля выбора device.
- при нескольких активных устройствах продукт пока не делает implicit best-effort routing; для deterministic push targeting позже понадобится отдельный explicit selection/delivery contract.
- расследование и reporting улучшаются: видно, когда challenge был approved/denied и каким device binding он был защищен.
