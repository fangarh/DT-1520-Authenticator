# ADR-007: MVP Technology Baseline

## Status

Accepted

## Context

Проекту нужен стек, который одновременно подходит для security-sensitive backend, коробочной поставки и нативного мобильного второго фактора, но не раздувает инфраструктуру на старте.

## Decision

Для `MVP` принимается такой baseline:

- backend: `ASP.NET Core`
- primary store: `PostgreSQL`
- fast runtime state: `Redis`
- persistence access: `Dapper`
- object mapping: `Mapperly`
- async execution: `Outbox + Background Worker`
- message broker: не использовать `RabbitMQ` в первой версии
- admin ui: `React + Vite`
- mobile: `Kotlin` для `Android`
- observability: `OpenTelemetry + Prometheus + Grafana + Loki + Tempo`

## Consequences

- инфраструктура `MVP` остается относительно простой
- коробочная версия проще во внедрении
- SQL и persistence слой остаются под явным контролем без `Entity Framework`
- mapping остается compile-time и прозрачно генерируемым через `Mapperly`
- мобильный клиент будет нативным на `Android`
- расширение на `iPhone` остается поздним отдельным треком
- `RabbitMQ` можно добавить позже без смены базовой доменной модели
