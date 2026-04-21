# ADR-032 - Push Delivery Uses Configurable Provider Adapter

## Status

Accepted

## Context

После реализации `auth.push_challenge_deliveries` и worker job `push_challenge_delivery` delivery contour уже стал outbox-driven и device-bound, но фактическая отправка сообщений все еще шла через logging-only gateway.

Этого было достаточно для локальной отладки и для изоляции delivery contract от provider-specific SDK/API, но уже недостаточно для pilot integration scenario: нужен реальный push provider, provider message id и fail-closed mapping retryable/non-retryable ошибок.

При этом `ADR-015` остается в силе: `push` не должен становиться обязательной внешней зависимостью для `on-prem` и future air-gapped профилей. Значит, новый adapter не должен ломать существующий outbox contract и должен сохранять controllable fallback path без внешнего provider.

## Decision

- delivery/outbox contract не меняется: `CreateChallenge`, `auth.push_challenge_deliveries`, lease/retry semantics и `IPushChallengeDeliveryGateway` остаются прежними
- фактическая отправка выносится в configurable provider adapter layer внутри `Infrastructure`
- provider выбирается через runtime config `PushDelivery:Provider`
- default/fallback provider остается `logging`, чтобы инсталляции без внешнего push могли продолжать локальную проверку без скрытого network dependency
- первым реальным provider-ом становится `FCM` через `HTTP v1 API`
- `FCM` использует `OAuth 2.0` service account credentials, scoped token retrieval и `POST https://fcm.googleapis.com/v1/projects/{projectId}/messages:send`
- credentials принимаются только как `service_account` JSON из `Base64`-строки или внешнего файла; другие credential types reject-ятся fail-closed на startup
- outbound payload остается sanitized:
  - без `pushToken` в logs/outbox
  - без `externalUserId`
  - без `correlationId`
  - с минимальным `challengeId + operationType` в `data` и generic notification copy
- `FCM` error mapping делится на:
  - retryable: timeout, transport error, `QUOTA_EXCEEDED`, `UNAVAILABLE`, `INTERNAL`
  - non-retryable: `UNREGISTERED`, `SENDER_ID_MISMATCH`, explicit unauthorized/rejected provider responses

## Consequences

- backend получает первый production-oriented provider adapter без изменения уже работающего `Device Registry` и outbox contour
- `provider_message_id` теперь может нести реальный provider response, а не synthetic logging identifier
- `on-prem` и dev профили сохраняют logging fallback вместо жесткой зависимости на внешний push provider
- следующий integration step после этого — не новый push transport, а observability/hardening и top-level provider-facing `webhooks/events`
