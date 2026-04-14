# ADR-015: Push Optional for On-Prem and Future Air-Gapped Profiles

## Status

Accepted

## Context

Часть `on-prem` заказчиков не сможет или не захочет зависеть от внешних `APNs/FCM`. Кроме того, в дальнейшем продукт должен поддержать fully air-gapped профиль, где внешние push-провайдеры недоступны по определению.

Если критический authentication path будет завязан на внешний `push`, это создаст архитектурный и security-риск для enterprise-поставки.

## Decision

- `push approval` остается важным фактором, но не является обязательной опорой продукта
- для `on-prem` и будущего fully air-gapped профиля `push` рассматривается как optional capability
- `TOTP` и `backup codes` должны оставаться полностью рабочими без внешних push-провайдеров
- fully air-gapped режим фиксируется как future enterprise requirement, но не входит в `MVP`

## Consequences

- критический путь аутентификации нельзя строить только вокруг `APNs/FCM`
- security-модель обязана поддерживать offline-safe факторы
- delivery и product planning должны учитывать профили, где `push` отключен или недоступен
- push adapters остаются расширяемой возможностью, а не жестким системным требованием
