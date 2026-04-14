# ADR-010: Monorepo Root Layout Without Top-Level Src

## Status

Accepted

## Context

В репозитории несколько самостоятельных рабочих корней: backend, mobile, admin, infra, config и knowledge vault.

Верхний `src/` добавляет лишний уровень вложенности и ухудшает читаемость путей.

## Decision

Верхний `src/` перестает быть каноническим кодовым корнем.

Целевые корни репозитория:

- `backend/`
- `mobile/`
- `admin/`
- `infra/`
- `config/`
- `OTP/`

Внутри `backend/` проекты лежат без дополнительного внутреннего `src/`.

## Consequences

- пути становятся короче и понятнее
- `.NET solution` лежит рядом с backend-проектами
- workspace лучше соответствует реальной структуре системы
- старый `src/` и внутренний `backend/src/` удалены после миграции
