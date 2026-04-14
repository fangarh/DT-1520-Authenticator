# ADR-019: FluentMigrator for PostgreSQL Schema Management

## Status

Accepted

## Context

После отказа от `Entity Framework` проекту все равно нужен контролируемый и повторяемый механизм эволюции схемы `PostgreSQL`.

Требования к этому механизму:

- не зависеть от `EF Core`
- быть пригодным для коробочной и on-prem поставки
- позволять запускать миграции отдельно от `Api`
- не хранить connection strings и секреты в репозитории
- поддерживать bootstrap среды: создание БД, накатывание схемы, отдельное сидирование bootstrap integration clients

## Decision

- для миграций `PostgreSQL` использовать `FluentMigrator`
- миграции запускать отдельным console project `backend/OtpAuth.Migrations`
- runtime `Api` не должен автоматически выполнять схему или сидирование security-sensitive данных при старте
- bootstrap среды выполнять явно через migration runner и отдельные команды:
  - `ensure-database`
  - `migrate`
  - `seed-bootstrap-clients`
- connection string брать только из `ConnectionStrings__Postgres`
- bootstrap client secrets не хранить в markdown, appsettings или исходном коде; сидирование читать их только из env vars

## Consequences

- схема БД становится versioned и управляется вне `Api`
- инфраструктурный bootstrap становится воспроизводимым без `EF` tooling
- migration runner можно безопасно использовать в `on-prem` поставке и CI/CD
- до явного сидирования bootstrap integration clients `/oauth2/token` не сможет выпускать токены, даже если схема уже поднята
