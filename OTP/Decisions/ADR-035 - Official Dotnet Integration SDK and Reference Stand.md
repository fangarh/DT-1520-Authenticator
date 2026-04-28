# ADR-035 - Official Dotnet Integration SDK and Reference Stand

## Status

Accepted

## Context

После первого ручного `ProjectManager` pilot стало понятно, что продукту нужен не только `REST` contract, но и официальный путь интеграции для `.NET`-экосистемы.

При этом `ProjectManager` не является удобным основным стендом для повторной диагностики latency: в нем есть `Keycloak`, собственный pending-operation слой, callback orchestration и production-like UI. Эти компоненты полезны для enterprise proof, но создают лишний шум при проверке базового integration loop.

Отдельно зафиксировано важное ограничение: `desktop`-приложение не должно хранить `integration client_secret` как confidential secret. Для обычного `desktop` сценария безопасная модель остается такой:

`Desktop App -> Backend -> DT-1520 Authenticator`

`Android` offline-code fallback в ближайшем плане означает только то, что `Android` генерирует `TOTP` без сети, а весь проверяющий стенд остается online и вызывает `DT-1520` для verification.

## Decision

Создаем официальный `.NET` integration SDK как отдельный productization track после закрытия operator-ready prerequisites:

1. `Admin Client Management` в backend/admin panel.
2. `QR Device Onboarding` для Android activation без debug/manual helper-ов.
3. `.NET` NuGet packages.
4. Reference `Desktop + Backend` стенд для повторного E2E цикла, latency hardening и online `TOTP` fallback.

SDK должен быть разделен по ответственности:

- `Dt1520.Authenticator.Client` - базовый typed client без `ASP.NET Core` зависимости.
- `Dt1520.Authenticator.AspNetCore` - DI, `IHttpClientFactory`, options binding, callback signature validation и backend integration helpers.
- `Dt1520.Authenticator.Desktop` - desktop UX/session/polling helpers без хранения `client_secret` в desktop app.

Reference stand становится основным диагностическим контуром для latency и fallback-поведения. `ProjectManager` остается enterprise pilot proof, но не основной стенд для отладки базового delivery/polling path.

## Consequences

- `Push Delivery Latency Follow-Up` переносит основной diagnostic target с `ProjectManager` на reference `Desktop + Backend` стенд.
- NuGet нельзя считать завершенным без README/API docs, samples и reference integration path.
- Desktop direct-to-Authenticator mode не входит в безопасный default path и требует отдельного решения, если понадобится.
- Документация становится частью Definition of Done для новых code slices: новые API, SDK, UI, installer и stand изменения должны обновлять соответствующие vault notes и public/developer docs в той же сессии.

