# Implementation Map

## Назначение

Этот файл нужен, чтобы новая сессия быстро понимала, где искать реальные артефакты, не сканируя весь репозиторий.

## Текущее содержимое репозитория

- `OTP/` — knowledge vault проекта и основная точка входа для контекста
- `backend/` — backend scaffold на `.NET`
- `mobile/` — Android scaffold на `Kotlin`
- `admin/` — admin scaffold на `React + Vite`
- `infra/` — инфраструктурный корень
- `config/mcp/` — локальные примеры MCP-конфигов

## Важные зоны внутри `OTP/`

- `OTP/00 - Start Here.md` — точка входа
- `OTP/01 - Current State.md` — фактическая стадия проекта
- `OTP/02 - Decision Index.md` — список принятых решений
- `OTP/03 - Open Questions.md` — незакрытые вопросы
- `OTP/Decisions/` — `ADR`
- `OTP/Architecture/` — канонические архитектурные заметки
- `OTP/Data/` — канонические заметки по данным и `ERD`
- `OTP/Integrations/` — интеграционный слой и `OpenAPI`
- `OTP/Product/` — продуктовые заметки и мобильный контур
- `OTP/Delivery/` — план внедрения и коробочная поставка
- `OTP/2FA/` — исторический набор исходных заметок по теме
- `OTP/Sessions/` — краткие записи по сессиям

## Как работать с этим файлом дальше

Текущие рабочие корни:

- `mobile` - Android project
- `backend/OtpAuth.sln` - основной backend solution entry point
- `admin/package.json` - admin workspace entry point
- `infra/` - инфраструктурный корень
- `config/mcp/` - локальные примеры MCP-конфигов

Текущее состояние entry points:

- `mobile` - проект создан в Android Studio
- `backend/OtpAuth.sln` - restore/build проходят
- `admin/package.json` - install/build проходят

Когда появится больше кода, сюда нужно добавить:

- путь к миграциям и схемам данных
- путь к тестам
- список ключевых entry points по модулям

## Последнее обновление

- `2026-04-14`: карта создана и привязана к scaffold-корням mobile, backend, admin, infra и MCP
- `2026-04-14`: канонические корни переключены с `src/*` на `mobile`, `backend`, `admin`
- `2026-04-14`: legacy-дубли и generated/cache хвосты удалены из рабочего дерева
