# Backend Module Design

## Status

Draft

## Цель

Разложить `modular monolith` на рабочие backend-модули, по которым можно начинать реализацию без смешивания ответственности.

## Рабочие модули `MVP`

- `Integration API`
- `2FA Core`
- `Factor Engine`
- `Device Registry`
- `Policy`
- `Audit`

## Принципы границ

- `Api` слой принимает transport contract и маппит его в application requests
- `Application` слой оркестрирует use cases
- `Domain` слой хранит правила, инварианты и модели
- `Infrastructure` слой реализует persistence, внешние adapters и delivery
- модуль владеет своей логикой и не читает чужую persistence-модель напрямую мимо контракта

## Правило для `enum`

- перед добавлением нового `enum` нужно проверять, не выражает ли он будущее поведение, которое лучше представить отдельными типами или стратегиями
- `enum` допустим для closed-set классификаций, transport mapping, persistence state и protocol codes
- если новый вариант начинает требовать собственного verify/enrollment/delivery поведения, это уже сигнал не расширять `enum`, а выносить поведение в отдельный объектный контракт
- в текущем backend после ревизии немедленного рефакторинга не требуется, но `FactorType` считается точкой повышенного внимания при дальнейшем развитии `Factor Engine`

## Модуль `Integration API`

### Ответственность

- аутентификация интеграционных клиентов
- прием вызовов `Challenges`, `Enrollments`, `Devices`
- маппинг transport-level contract в use cases

### Основные use cases

- `CreateChallenge`
- `GetChallengeStatus`
- `VerifyTotp`
- `StartTotpEnrollment`
- `ActivateDevice`

### Не должен делать

- не содержит доменных правил фактора
- не принимает решение по policy сам
- не работает напрямую с `Redis` и `PostgreSQL` в обход application layer

## Модуль `2FA Core`

### Ответственность

- orchestration жизненного цикла `challenge`
- выбор следующего шага с учетом policy и factor availability
- фиксация итогового статуса

### Базовые агрегаты и сущности

- `Challenge`
- `ChallengeAttempt`

### Доменные события

- `ChallengeCreated`
- `ChallengeApproved`
- `ChallengeDenied`
- `ChallengeExpired`

## Модуль `Factor Engine`

### Ответственность

- `TOTP` verification
- `backup code` verification
- запуск `push challenge`

### Внутренние зоны

- `Totp`
- `BackupCodes`
- `PushApproval`

### Инварианты

- factor verification не раскрывает секреты наружу
- повторное использование одноразового артефакта недопустимо
- каждый verify path проходит через anti-replay и rate limit policy

## Модуль `Device Registry`

### Ответственность

- activation flow
- хранение delivery tokens
- revoke и trust status
- привязка устройства к пользователю

### Состояния устройства

- `pending`
- `active`
- `revoked`
- `blocked`

## Модуль `Policy`

Подробный рабочий контракт: [[Policy Design]]

### Ответственность

- выбор обязательности второго фактора
- выбор допустимых факторов для клиента и пользователя
- ограничения по deployment profile
- проверка допустимости `push` и device trust state
- будущая точка расширения для risk-based правил

### Принципы `MVP`

- отдельный модуль внутри монолита, но не внешний сервис
- code-first rules с ограниченной конфигурацией
- `deny by default`, если входной контекст неполный
- policy result должен быть explainable и auditable

### Вход policy evaluation

- `tenant`
- `application client`
- `operation type`
- `user context`
- `device trust state`
- `deployment profile`
- `available factors`

### Выход policy evaluation

- требуется ли второй фактор
- какие факторы разрешены
- разрешен ли `push`
- нужен ли fallback на `TOTP`
- допускается ли enrollment action

### Для `MVP`

- правила лучше сделать простыми и конфигурируемыми
- полноценный отдельный rule engine не вводить раньше необходимости
- не позволять произвольные admin-authored правила без отдельного этапа hardening

## Модуль `Audit`

### Ответственность

- фиксация security-значимых событий
- подготовка событий для расследования
- экспорт в observability и внешние контуры позже

### Инварианты

- не хранить секреты и токены в payload
- события должны быть append-only на уровне application flow

## Вертикальный срез `MVP`

### Slice 1

- `CreateChallenge`
- `GetChallenge`
- `VerifyTotp`

### Что должно появиться в коде

- endpoint contract
- application handlers
- domain rules для `Challenge` и `TOTP verification`
- persistence mapping
- unit tests
- integration tests
- security tests для auth, validation, rate limiting и secret leakage

## Маппинг на текущую структуру репозитория

- `backend/OtpAuth.Api` - transport endpoints и composition root
- `backend/OtpAuth.Application` - use cases и интерфейсы портов
- `backend/OtpAuth.Domain` - агрегаты, value objects, доменные сервисы
- `backend/OtpAuth.Infrastructure` - persistence, crypto, token services, adapters
- `backend/OtpAuth.Worker` - outbox и delivery фоновых задач

## Следующий шаг

После фиксации `ADR-011`-`ADR-017` этот документ должен быть уточнен до списка конкретных namespaces, папок и use case contracts.

Следующая заметка для детализации: [[Policy Design]]

## Текущий кодовый прогресс

- в `backend/OtpAuth.Application/Challenges` уже реализованы `CreateChallengeHandler`, `GetChallengeHandler` и `VerifyTotpHandler`
- в `backend/OtpAuth.Api/Endpoints/ChallengesEndpoints.cs` опубликованы `POST /api/v1/challenges`, `GET /api/v1/challenges/{id}` и `POST /api/v1/challenges/{id}/verify-totp`
- `Challenge` persistence переведена на `backend/OtpAuth.Infrastructure/Challenges/PostgresChallengeRepository.cs`
- `Factor Engine` уже использует enrollment-backed `TOTP` verifier через `backend/OtpAuth.Infrastructure/Factors/PostgresTotpVerifier.cs`
- `VerifyTotp` пишет append-only записи в `challenge_attempts`
- следующий практический шаг для этого slice: добавить rate limit/anti-replay persistence и полноценный enrollment API
