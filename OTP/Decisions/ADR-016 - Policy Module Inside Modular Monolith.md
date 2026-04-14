# ADR-016: Policy Module Inside Modular Monolith

## Status

Accepted

## Context

Платформе уже в `MVP` нужен единый слой принятия решений по обязательности второго фактора, допустимым факторам, ограничениям deployment profile и trust state устройства.

Если policy-логика будет размазана по `API`, `Challenge`, `Device Registry`, `Admin` и `Factor Engine`, проект быстро потеряет управляемость, а security-ошибки станут труднее обнаруживать и аудировать.

При этом полноценный внешний `Policy Engine`, DSL или отдельный сервис в `MVP` создаст избыточную сложность: больше operational overhead, труднее explainability, больше точек отказа и более дорогой debugging.

## Decision

- в `MVP` вводится отдельный модуль `Policy` внутри `modular monolith`
- `Policy` не выносится в отдельный сервис
- `Policy` не требует отдельного DSL или универсального rule engine в первой версии
- правила первой версии реализуются как code-first policy subsystem с ограниченной конфигурацией
- policy evaluation должна быть детерминированной, auditable и работать по принципу `deny by default`

## Consequences

- backend получает явную архитектурную границу для security- и access-related решений
- правила выбора факторов и разрешенных действий не размазываются по другим модулям
- `Policy` можно отдельно покрывать unit и integration tests
- позже модуль можно расширить до более сложного engine, если появится реальная потребность в динамических правилах
