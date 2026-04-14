# ADR-017: Prefer Types with Behavior over Enums for Extensible Domain Concepts

## Status

Accepted

## Context

В проекте уже появились `enum` для factor type, operation type, trust state, deployment profile и других категорий.

`Enum` удобен для closed-set классификаций, transport mapping и persistence state. Но он становится источником проблем, когда через него начинают кодировать поведение: появляются `switch` по всему коду, логика размазывается по модулям, а добавление нового варианта требует правок во многих местах.

Для платформы `2FA/MFA`, которая должна эволюционировать в сторону `IdP`, особенно опасны случаи, где `enum` скрывает будущий полиморфизм: factor-specific verification, enrollment flow, delivery behavior, recovery semantics.

## Decision

- перед введением нового `enum` обязательно проверять, не описывает ли он поведение, которое лучше выразить отдельными типами, стратегиями или иерархией объектов
- `enum` допустим только для действительно закрытых и простых классификаций:
  - transport/persistence states
  - protocol-facing codes
  - deployment/environment flags
  - bounded policy inputs, если сами правила не живут в `enum`
- если новый вариант должен не только храниться, но и вести себя по-разному, по умолчанию предпочитать polymorphism over branching
- `switch` по `enum` допустим в boundary/mapping code и в ограниченных orchestration rules, но не как способ размазывать доменное поведение по системе
- при ревизии текущего кода считать `FactorType` и будущие factor-specific flows зоной повышенного внимания: если factor logic начнет расходиться по обработчикам, это сигнал выносить поведение в отдельные типы/стратегии

## Consequences

- архитектурные решения по новым типам становятся более осознанными и не скатываются в enum-driven design
- простые state/status коды можно продолжать выражать через `enum` без лишнего усложнения
- при развитии `Factor Engine`, enrollment и recovery flows команда должна раньше замечать момент, когда classification перестает быть просто classification
- текущие `enum` в backend не требуют немедленного рефакторинга, но дальнейшее расширение `FactorType` и factor-specific behavior должно проходить через отдельную проверку на полиморфизм
