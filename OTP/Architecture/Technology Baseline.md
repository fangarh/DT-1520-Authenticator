# Technology Baseline

## Утвержденный baseline для MVP

### Backend

- `ASP.NET Core`
- `PostgreSQL`
- `Redis`
- `Dapper`
- `Mapperly`
- `FluentMigrator`
- `Outbox + Background Worker`
- без `RabbitMQ` в первой версии

### Admin UI

- `React`
- `Vite`

### Mobile

- `Kotlin` для `Android`
- `iPhone` не входит в первый обязательный контур
- поддержка `iPhone` рассматривается как поздний отдельный трек после стабилизации `Android` и backend

### Observability

- `OpenTelemetry`
- `Prometheus`
- `Grafana`
- `Loki`
- `Tempo`

## Архитектурная позиция

- backend остается `modular monolith`
- `PostgreSQL` является источником истины
- `Redis` используется только как быстрый runtime-слой
- `Entity Framework` не используется
- persistence слой проектируется вокруг явного SQL через `Dapper`
- object mapping проектируется вокруг `Mapperly` source generation
- schema evolution проектируется вокруг отдельного `FluentMigrator` runner
- асинхронность в `MVP` строится через `outbox`, worker и retry-механику
- `RabbitMQ` не вводится до появления реальной необходимости

## Что сознательно откладываем

- `RabbitMQ`
- микросервисы
- `iPhone` client
- сложный брокерный event backbone
- ранний переход к native `iOS`

## Почему это решение pragmatic

- минимизирует инфраструктурную сложность коробочной версии
- не усложняет сопровождение на раннем этапе
- дает нативный `Android` клиент для security-sensitive сценария
- не мешает позже добавить `RabbitMQ`, `iOS` и более сложную delivery-модель
