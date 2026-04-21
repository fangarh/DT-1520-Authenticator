# ADR-033 - Top-Level Webhooks Use Subscription Outbox

## Status

Accepted

## Context

После operation-level `challenge callbacks` платформа уже умеет отправлять внешние уведомления, но этот контур остается привязанным к одному `CreateChallenge` request и не закрывает top-level provider-facing event model из `OpenAPI`.

Нужен отдельный subscription/event contour для platform events, который живет независимо от per-request callback URL, имеет свою outbox-модель, worker-driven delivery и одинаковую signing semantics для webhook consumers.

При этом текущий шаг не должен пытаться сразу закрыть весь event catalog и полноценный management API/UI. Нужен минимальный working slice, который вводит subscriptions, outbox и delivery для top-level `challenge.*`, а `device.*` и `factor.*` наращивает уже поверх этого каркаса.

## Decision

- top-level `webhooks` реализуются отдельной subscription model, а не reuse existing `callback.url`
- persistence разделяется на:
  - `auth.webhook_subscriptions`
  - `auth.webhook_subscription_event_types`
  - `auth.webhook_event_deliveries`
- terminal `challenge.*` state changes атомарно fan-out-ят sanitized event snapshot в matching subscriptions через outbox rows
- первый implementation slice ограничен `challenge.approved`, `challenge.denied` и `challenge.expired`
- dispatch идет через отдельный worker job `webhook_event_delivery` с lease/retry/fail semantics
- подпись outbound webhook остается `HMAC-SHA256` в `X-OTPAuth-Signature`
- на текущем этапе signing key задается глобально через runtime config `Webhooks:SigningKey`, без per-subscription secret storage
- bootstrap registration path пока operational-only:
  - `list-webhook-subscriptions`
  - `upsert-webhook-subscription <client-id> <endpoint-url> <event-type> [event-type...]`
- subscription endpoint validation fail-closed требует `HTTPS` и reject-ит `localhost`/private-network IP literals

## Consequences

- platform-level event model отделен от operation-level callbacks и может развиваться независимо
- `challenge.*` terminal events теперь имеют и per-request callback path, и top-level subscription path
- outbox хранит payload snapshot и не зависит от повторной загрузки mutable domain state при dispatch
- `device.*` и `factor.*` события можно добавлять дальше поверх того же subscription/outbox contour
- management API/UI для webhook subscriptions остается следующим шагом; текущий bootstrap path сознательно ограничен migration runner commands
