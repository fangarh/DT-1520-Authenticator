# Admin and Android Readiness Gates

## Status

Accepted working guideline

## Цель

Зафиксировать, когда команда может безопасно начинать `admin UI` и `Android`, не опираясь на нестабильный backend-контур.

## Admin UI readiness

`Admin UI` можно начинать, когда backend закрывает полный минимальный lifecycle для операторского `TOTP`-фактора:

- `start / confirm / revoke / replace` для `TOTP` enrollment
- read path для текущего enrollment state
- стабильные response/error contracts для enrollment endpoints
- endpoint-level tests на `401/403/404/409` и scope enforcement
- security-аудит: секреты не возвращаются повторно после создания и не попадают в audit/log payload

До выполнения этих условий `admin UI` будет опираться на временный API и создаст лишний передел.

## Android readiness

Полноценный `Android`-клиент рано начинать до появления device lifecycle и `push` backend contour.

Но можно начинать `Android TOTP-first slice`, когда есть:

- стабильный enrollment artifact flow
- понятный provisioning contract
- зафиксированная модель локального secure storage
- базовые контракты ошибок для enrollment/activation path

Канонический provisioning contract для текущего `TOTP-first` слайса зафиксирован в [[../Integrations/TOTP Provisioning Contract]].

## Что можно делать для Android уже сейчас

- app skeleton и module layout
- secure storage abstraction поверх `Keystore`
- локальный `TOTP` generator
- enrollment/provisioning экран для `TOTP`
- unit tests и базовые UI tests для `TOTP` flow

## Что для Android еще рано

- полноценный `push approval`
- device activation/revoke lifecycle
- production-ready session/token lifecycle для устройства
- operator/device support flows end-to-end

## Что должно быть готово перед полным Android phase

- backend device registry и activation flow
- revoke/block lifecycle для устройства
- backend/API contract для `push challenge`
- audit и security controls для device token lifecycle
- тестовый контур для mobile-to-backend integration

## Что можно делать параллельно

- `admin UI` implementation поверх уже стабилизированного `TOTP` enrollment management API
- `Android` architecture skeleton и `TOTP-first` локальные модули
- `backup codes` backend slice
- device lifecycle design/contracts
- enrollment API contract review against future `admin` screens

## Рекомендуемый порядок

1. Начать `admin UI` поверх готового enrollment management backend.
2. Параллельно начать `Android TOTP-first`.
3. Следующим backend slice взять `backup codes`.
4. После этого переходить к device lifecycle и `push`.
