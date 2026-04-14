# ADR-011: Single-Tenant Deployment with Tenant-Aware Domain Model

## Status

Accepted

## Context

Продукт должен поддерживать и облачный, и коробочный режим из одной кодовой базы. При этом ранний `MVP` не должен усложняться полноценной multi-tenant операционной моделью, если это не дает немедленной ценности.

Одновременно опасно полностью игнорировать tenant boundary на уровне домена и данных, потому что это приведет к дорогостоящей переделке `ApplicationClient`, `User`, `Challenge`, `Audit` и auth flow.

## Decision

- `MVP` поставляется как `single-tenant` deployment per installation
- доменная модель и данные остаются `tenant-aware` с первого дня
- каждый integration client, user, challenge, audit event и policy scope привязывается к `tenant_id`
- в `MVP` не вводится сложная cross-tenant admin модель и shared multi-tenant runtime isolation

## Consequences

- коробочная поставка и первая реализация остаются проще операционно
- backend с первого дня обязан проводить tenant scoping в application и persistence слоях
- security-модель получает явную границу изоляции данных
- будущий переход к hosted multi-tenant mode не потребует переписывать базовую доменную модель
