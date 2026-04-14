# Open Questions

## Product

- Нужен ли multi-tenant режим уже в `MVP`, или сначала single-tenant коробка?
- Нужен ли self-service enrollment в первой версии, или только администраторский onboarding?
- Будет ли продукт выступать только как `2FA provider`, или позже как полноценный `IdP`?

## Architecture

- Сразу ли вводить очередь сообщений, или оставить асинхронность внутри процесса до появления реальной нагрузки?
- Нужен ли отдельный `Policy Engine` модулем уже в первом релизе?

## Mobile

- Нужна ли обязательная device attestation в первой версии?
- Когда именно `iPhone` должен перейти из growth track в обязательный delivery scope?

## Delivery

- Требуется ли fully air-gapped `on-prem` режим?
- Можно ли опираться на внешние `APNs/FCM` во всех целевых сценариях коробки?
