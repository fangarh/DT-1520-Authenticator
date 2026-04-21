# Implementation Plan 8-12 Weeks

## Цель

За 8-12 недель довести проект от стартового архитектурного каркаса до работающего `MVP`.

## Актуализация статуса на `2026-04-17`

- `Phase 1` фактически закрыта
- `Phase 2` фактически закрыта
- `Phase 3` фактически закрыта
- `Phase 4` закрыта частично: `Admin UI MVP` и `Installer MVP` уже реализованы, но остаются `webhook/events`, observability и часть support/productization flows
- `Phase 5` в полном объеме еще не закрыта и сейчас вместе с хвостами `Phase 4` образует основной remaining gap до полного `MVP`

Для канонического continuation path после `2026-04-20` использовать [[MVP Closure Iteration Plan]] и [[ProjectManager Pilot Integration Story]], а не расширять этот исторический план новыми ad-hoc фазами.

## Phase 1. Foundation

Недели `1-3`.

- выбрать backend stack
- собрать skeleton `modular monolith`
- внедрить `PostgreSQL` и `Redis`
- реализовать сущности `Tenant`, `ApplicationClient`, `User`, `Challenge`
- собрать аудит и базовые политики

## Phase 2. TOTP

Недели `3-5`.

- enrollment `TOTP`
- проверка `TOTP`
- backup codes
- минимальный `Admin API`

## Phase 3. Mobile Push

Недели `5-8`.

- регистрация устройства
- `push challenge`
- `approve / deny`
- базовая биометрия в приложении
- отзыв устройства

## Phase 4. Productization

Недели `8-10`.

- админка
- операторские enrollment и support flows
- `Docker Compose`
- webhook-события
- метрики и алерты

## Phase 5. Hardening

Недели `10-12`.

- threat review
- rate limiting
- секреты и ротация ключей
- резервное копирование
- пилотный интеграционный сценарий

## Не брать в этот интервал

- `SAML`
- `RADIUS`
- полный `OIDC provider`
- обязательный self-service enrollment
- сложный risk engine
- преждевременный переход к микросервисам
