# Documentation Session Starter Prompt

## Status

Ready to use

## Purpose

Этот промт нужен для отдельной Codex-сессии, которая должна создать полноценную документацию проекта в `docs/`, включая установку, настройку и работу `Admin UI`.

## Prompt

```text
Ты работаешь в репозитории `D:\Projects\2026\DT-1520-Authenticator`.

Всегда отвечай на русском. Работай vault-first и сначала прочитай:

1. `OTP/00 - Start Here.md`
2. `OTP/01 - Current State.md`
3. `OTP/02 - Decision Index.md`
4. `OTP/Agent/Implementation Map.md`
5. `OTP/Delivery/Documentation Handoff Plan.md`
6. `OTP/Delivery/Official Dotnet Integration SDK.md`
7. `OTP/Delivery/Reference Desktop Backend Stand.md`
8. `OTP/Delivery/MVP Closure Iteration Plan.md`
9. `AGENTS.md`

Задача: создать первую полноценную документацию проекта в папке `docs/` как отдельно стоящую React-страницу без явного backend.

Контекст repo roots:

- `backend/` - основной `.NET` backend runtime
- `admin/` - runtime `Admin UI MVP` на React/Vite
- `mobile/` - Android приложение
- `infra/` - Docker Compose packaging, installer scripts and deployment assets
- `installer-ui/` - local installer UI
- `lib/` - будущие NuGet библиотеки
- `rdb_stand/` - будущий reference Desktop + Backend stand
- `docs/` - documentation app / documentation source
- `OTP/` - source of truth для решений и проектного контекста

Нужно сделать documentation-first срез, не реализуя новый runtime функционал:

1. Проанализировать текущие entry points из `Implementation Map` и точечно прочитать код/конфиги, необходимые для точной документации.
2. Спроектировать структуру документации в `docs/` как standalone React/Vite documentation app, который собирается в статический `dist/` и не требует backend.
3. Создать/обновить документационные страницы по минимуму:
   - обзор продукта и архитектуры;
   - локальная установка и запуск;
   - установка через Docker/infra contour;
   - первичная настройка backend/runtime env;
   - установка и настройка `Admin UI`;
   - создание/настройка admin user;
   - вход в admin panel;
   - настройка integration client lifecycle, если код уже реализован; если еще не реализован - явно отметить как planned track;
   - enrollment/user/device operator workflows;
   - webhook subscription and delivery visibility workflows;
   - Android activation/provisioning/push/TOTP flows;
   - security model and secret boundaries;
   - troubleshooting;
   - SDK/reference stand placeholders with links to `lib/` and `rdb_stand/`.
4. Для `Admin UI` обязательно покрыть:
   - prerequisites;
   - backend/API dependency;
   - environment/config expectations;
   - admin user bootstrap;
   - login/session/CSRF model;
   - permissions;
   - enrollment workspace;
   - webhook workspace;
   - delivery statuses workspace;
   - user devices workspace;
   - безопасное обращение с one-time secret/provisioning artifacts;
   - known limitations and planned admin client management.
5. Не раскрывать реальные secrets, токены, private credentials, signing material, `pushToken`, raw callback payloads или приватные URL с credentials.
6. Не выдавать debug-only Android activation helper как production flow.
7. Все новые docs должны ссылаться на реальные repo paths, команды и vault notes.
8. React/docs app код обязателен: соблюдай project frontend rules, не делать один общий CSS-файл, разделять UI/logic/styles, компоненты до 300 строк, проверять внешний вид через Playwright.
9. После изменений обновить:
   - `OTP/Delivery/Documentation Handoff Plan.md`
   - `OTP/Agent/Implementation Map.md`
   - `OTP/01 - Current State.md`
   - latest session note under `OTP/Sessions/`

Definition of Done:

- `docs/` содержит отдельно стоящую React documentation page и минимум контента для передачи коллегам.
- Документация покрывает установку и настройку admin panel.
- Документация не расходится с текущим кодом и vault.
- Security review проведен и все замечания исправлены.
- Выполнены unit tests, production build и Playwright visual/browser verification для React documentation app.
```

## Notes for the next session

- Фактическое имя папки стенда сейчас `rdb_stand`, хотя в обсуждении использовалось название `rdb-stand`.
- Если пользователь переименует папку, сначала обновить `Documentation Handoff Plan`, `Implementation Map` и эту заметку.
- Перед созданием React documentation app использовать Context7 для актуальной документации выбранного frontend стека.
