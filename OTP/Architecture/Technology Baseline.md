# Technology Baseline

## Утвержденный baseline для MVP

### Backend

- `ASP.NET Core`
- `PostgreSQL`
- `Redis`
- `Outbox + Background Worker`
- без `RabbitMQ` в первой версии

### Admin UI

- `React`
- `Vite`

### Mobile

- `Kotlin` для `Android`
- `iPhone` не входит в первый обязательный контур
- поддержка `iPhone` рассматривается как точка роста

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
