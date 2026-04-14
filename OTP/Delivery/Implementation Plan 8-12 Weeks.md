# Implementation Plan 8-12 Weeks

## Цель

За 8-12 недель довести проект от архитектурного каркаса до работающего `MVP`.

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
- self-service flows
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
- сложный risk engine
- преждевременный переход к микросервисам
