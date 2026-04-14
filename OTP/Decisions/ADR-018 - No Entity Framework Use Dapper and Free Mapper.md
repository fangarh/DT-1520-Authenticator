# ADR-018: No Entity Framework, Use Dapper and Free Mapper

## Status

Accepted

## Context

Backend проекта должен оставаться предсказуемым по SQL, прозрачным для security review и удобным для коробочной эксплуатации.

Для этого нежелательно строить persistence слой вокруг `Entity Framework`: он добавляет лишний runtime abstraction layer, усложняет контроль над SQL, миграциями и performance-профилем, а также повышает риск незаметной магии в security-sensitive коде.

Одновременно проекту все еще нужен удобный и повторяемый mapping между persistence models, application DTO и domain-моделями.

## Decision

- в проекте не использовать `Entity Framework` и `EF Core`
- persistence слой backend строить на `Dapper`
- для object mapping использовать `Mapperly` (`Riok.Mapperly`)
- эволюцию схемы и database bootstrap строить отдельно через migration tool, а не через `EF`
- выбор `Mapperly` основан на лицензии `Apache-2.0`, допускающей коммерческое использование, и на compile-time source generation без runtime reflection
- mapper не должен вводить тяжелую runtime-магии поверх critical path persistence logic
- SQL, scoping и параметры запросов должны оставаться явно контролируемыми в коде

## Consequences

- backend persistence проектируется вокруг явных SQL-запросов и ручного контроля схемы
- миграции и database bootstrap строятся отдельно, без `EF` tooling; текущее решение для этого вынесено в отдельный `ADR-019`
- слой mapping строится вокруг `Mapperly` generated code и остается прозрачным для code review
- все будущие persistence-решения должны исходить из `Dapper + free mapper`, если пользователь не примет отдельное новое решение
