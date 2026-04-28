# Documentation Handoff Plan

## Status

Accepted working rule and preparation note

## Goal

Подготовить проект к отдельной documentation session, в которой нужно будет покрыть уже написанные backend, admin, mobile, installer, SDK и reference stand контуры документацией для передачи коллегам.

## New project rule

С этого момента для новых code tasks документация входит в Definition of Done.

Каждая существенная реализация должна в той же сессии обновлять:

- профильную заметку в `OTP/`
- `OTP/01 - Current State.md`, если изменился implementation status
- `OTP/Agent/Implementation Map.md`, если появились новые entry points
- relevant ADR, если принято long-lived архитектурное решение
- developer/user documentation under the appropriate docs surface, when the code is user-facing or integration-facing
- latest session note

## Documentation surfaces to create or complete

Фактические repo roots на `2026-04-27`:

- `docs/` - standalone `React + Vite` documentation app без backend dependency
- `lib/` - будущие NuGet библиотеки
- `rdb_stand/` - будущий reference `Desktop + Backend` stand

В обсуждении использовалось имя `rdb-stand`, но в рабочем дереве фактическая папка называется `rdb_stand`. Если она будет переименована, нужно обновить эту заметку, [[../Agent/Implementation Map]] и [[Documentation Session Starter Prompt]].

## Required documentation inventory

### Backend runtime

Нужно описать:

- challenge lifecycle
- `TOTP` verification
- backup code verification
- device activation/refresh/revoke lifecycle
- push approve/deny and delivery
- callback delivery
- top-level webhooks/events
- admin auth, `CSRF` and permissions
- integration client lifecycle
- security audit events
- migrations and operational commands
- worker jobs and heartbeat diagnostics

### Admin UI

Нужно описать:

- operator login/session model
- enrollment workspace
- webhook subscription workspace
- delivery statuses workspace
- user devices workspace
- integration client management workspace
- one-time secret/provisioning artifact display rules

### Android app

Нужно описать:

- `TOTP` account storage and code generation
- provisioning/import flow
- device session storage
- push approval inbox
- biometric-gated approve path
- decision history
- future QR onboarding flow
- debug-only pilot activation helper and why it must not be production path

### Installer and deployment

Нужно описать:

- compose profile
- ghostring profile
- installer `install/update/recover`
- structured JSON reports
- diagnostics and worker heartbeat
- operational runbooks
- secret boundary

### Integration SDK and reference stand

Нужно описать:

- NuGet package layout
- getting started backend integration
- desktop-safe topology
- callback validation
- `TOTP` fallback
- error/result model
- package versioning
- sample reference flow

## Documentation quality requirements

- Документация не должна раскрывать secrets, raw tokens, signing material, `pushToken`, private URLs with credentials or raw callback payloads.
- Все integration docs должны явно показывать security boundary.
- Все API examples должны соответствовать текущему OpenAPI/vault contract.
- Public-facing docs не должны ссылаться на debug-only paths как production guidance.
- Для каждого user-facing workflow нужен short happy path and failure handling.
- Для каждого operator workflow нужен permissions and audit note.

## Suggested documentation app sections

- Overview
- Architecture
- Admin Guide
- Integration Guide
- .NET SDK
- Android Guide
- Deployment
- Operations
- Security
- Troubleshooting
- Changelog / compatibility

## Documentation session reading path

1. [[../00 - Start Here]]
2. [[../01 - Current State]]
3. [[../Agent/Implementation Map]]
4. this note
5. [[Official Dotnet Integration SDK]]
6. [[Reference Desktop Backend Stand]]
7. [[MVP Closure Iteration Plan]]
8. source code entry points from `Implementation Map`
9. [[Documentation Session Starter Prompt]]

## Immediate documentation backlog

- Replace historical `Documentation Backlog` with current productization docs structure.
- Add SDK package README templates under `lib/`.
- Add reference stand runbook under `rdb_stand/`.
- Add documentation app information architecture under `docs/`.
- Create admin panel installation/configuration documentation as first practical doc slice.
- Audit existing vault notes for outdated implementation status.

## First documentation slice

Первый отдельный documentation task должен начинаться со [[Documentation Session Starter Prompt]].

Минимальный ожидаемый результат:

- standalone React documentation page in `docs/` without an explicit backend
- admin panel installation and setup guide
- backend/runtime setup guide required for admin panel
- operator workflows guide
- security/secret boundary notes
- placeholders for `lib/` SDK docs and `rdb_stand/` runbook

## Current documentation app slice

Status on `2026-04-27`:

- `docs/` теперь содержит отдельный static `React + Vite` documentation app без backend dependency.
- `docs/src/data/documentation.ts` хранит typed handoff content по overview, setup, deployment, `Admin UI`, `Android`, security и SDK/reference roadmap.
- `docs/src/app` и `docs/src/shared` разделяют page shell, UI sections и CSS modules; backend/API layer не добавлялся.
- `docs/e2e/docs-page.spec.ts` проверяет desktop/mobile render и отсутствие page-level horizontal overflow.
- Verification закрыта через `npm test`, `npm run build` и `npm run test:e2e`.
- Security review: реальные secrets/tokens/signing material/private credential URLs/raw callback payloads/`pushToken` в docs app не добавлены; debug-only Android activation helper явно помечен как non-production pilot tooling.

## Admin client management documentation closure

Status on `2026-04-27`:

- `docs/src/data/documentation.ts`, `docs/README.md` и `admin/README.md` обновлены под закрытый operator-ready integration client lifecycle.
- Документация фиксирует permissions `integration-clients.read|write`, create/rotate one-time `clientSecret`, whitelist-only scopes, deactivate/reactivate, token invalidation и troubleshooting.
- Security review: документация не добавляет реальные secrets, raw tokens, signing material, private credential URLs, raw callback payloads или `pushToken`.

## SDK ASP.NET Core helper documentation checkpoint

Status on `2026-04-27`:

- `lib/README.md` and `lib/src/Dt1520.Authenticator.AspNetCore/README.md` now document `AddDt1520Authenticator(...)`, options binding/validation, named `IHttpClientFactory` registration and raw-body callback validation.
- `docs/src/data/documentation.ts` and `docs/README.md` include the ASP.NET Core helper status in the SDK handoff section.
- Security review: docs keep `ClientSecret` and `CallbackSigningSecret` as server-side placeholders only, do not include real secrets/tokens/signing material/raw callback payloads, and preserve the rule that protected business changes are committed only after approved DT-1520 results.

## SDK Desktop helper documentation checkpoint

Status on `2026-04-27`:

- `lib/README.md` and `lib/src/Dt1520.Authenticator.Desktop/README.md` now document desktop approval session polling against an integrator backend only.
- `docs/src/data/documentation.ts` and `docs/README.md` include the Desktop helper status in the SDK handoff section.
- Security review: docs preserve `Desktop App -> Integrator Backend -> DT-1520 Authenticator`, do not add desktop-held `client_secret`, bearer token, direct DT-1520 base URL, raw callback payload, QR activation payload or mobile device token examples.
