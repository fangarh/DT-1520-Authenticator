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
- `OTP/Security/` — канонические security-ограничения и защитная модель `MVP`
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
- список ключевых entry points по модулям
- mapping модулей `Backend Module Design` на реальные namespaces и проекты

Текущие test entry points:

- `backend/OtpAuth.Infrastructure.Tests` - unit tests для `Policy`, `Challenge` handlers, persistence, bootstrap OAuth, `TOTP` crypto/verifier и `VerifyTotp` runtime protection
- `backend/OtpAuth.Migrations` - migration runner для `PostgreSQL` (`ensure-database`, `migrate`, `seed-bootstrap-clients`, `seed-bootstrap-totp-enrollment`)

Текущие backend entry points по коду:

- `backend/OtpAuth.Api/Endpoints/AuthEndpoints.cs` - `POST /oauth2/token` для bootstrap `client_credentials`
- `backend/OtpAuth.Api/Endpoints/AuthEndpoints.cs` - `POST /oauth2/token`, `POST /oauth2/introspect`, `POST /oauth2/revoke`
- `backend/OtpAuth.Api/Endpoints/ChallengesEndpoints.cs` - `POST /api/v1/challenges`, `GET /api/v1/challenges/{id}` и `POST /api/v1/challenges/{id}/verify-totp`
- `backend/OtpAuth.Application/Challenges/CreateChallengeHandler.cs` - create use case с вызовом `Policy`
- `backend/OtpAuth.Application/Challenges/GetChallengeHandler.cs` - read use case для retrieval по `challengeId`
- `backend/OtpAuth.Application/Challenges/VerifyTotpHandler.cs` - verify use case с переводом `Challenge` в `approved`, `failed` или `expired`
- `backend/OtpAuth.Application/Challenges/IChallengeAttemptRecorder.cs` - append-only порт для фиксации verify-attempts
- `backend/OtpAuth.Application/Integrations/*` - integration client contracts, token issuance, scopes и client context
- `backend/OtpAuth.Application/Integrations/*` - integration client credential validation, token issuance, introspection, revocation и runtime validation contracts
- `backend/OtpAuth.Infrastructure/Challenges/PostgresChallengeRepository.cs` - `PostgreSQL`-backed persistence для `Challenge`
- `backend/OtpAuth.Infrastructure/Challenges/ChallengeDataMapper.cs` - `Mapperly` mapping для `Challenge` persistence model
- `backend/OtpAuth.Infrastructure/Challenges/PostgresChallengeAttemptRecorder.cs` - append-only запись verify-attempts в `PostgreSQL`
- `backend/OtpAuth.Infrastructure/Factors/PostgresTotpEnrollmentStore.cs` - загрузка активного `TOTP` enrollment из `PostgreSQL`
- `backend/OtpAuth.Infrastructure/Factors/TotpSecretProtector.cs` - rotation-ready key ring для `TOTP`: current + legacy keys по `key version`
- `backend/OtpAuth.Infrastructure/Factors/PostgresTotpVerifier.cs` - enrollment-backed `TOTP` verification
- `backend/OtpAuth.Infrastructure/Factors/PostgresTotpReplayProtector.cs` - persistent anti-replay reservation для использованных `TOTP` time step
- `backend/OtpAuth.Infrastructure/Factors/PostgresTotpVerificationRateLimiter.cs` - runtime rate limiting для `VerifyTotp` на основе `challenge_attempts`
- `backend/OtpAuth.Infrastructure/Factors/TotpSecretsReEncryptionService.cs` - maintenance workflow для re-encryption `TOTP` secrets на current `key version`
- `backend/OtpAuth.Infrastructure/Factors/PostgresTotpEnrollmentMaintenanceStore.cs` - пакетная загрузка и обновление encrypted `TOTP` enrollment-ов для maintenance-операций
- `backend/OtpAuth.Infrastructure/Integrations/PostgresIntegrationClientStore.cs` - `PostgreSQL`-backed registry интеграционных клиентов для OAuth
- `backend/OtpAuth.Infrastructure/Integrations/PostgresRevokedIntegrationAccessTokenStore.cs` - persistent revoked-token store для integration access tokens
- `backend/OtpAuth.Infrastructure/Integrations/IntegrationAccessTokenRuntimeValidator.cs` - runtime enforcement revoked/inactive integration tokens
- `backend/OtpAuth.Infrastructure/Integrations/PostgresIntegrationClientSeeder.cs` - explicit bootstrap seed для integration clients
- `backend/OtpAuth.Infrastructure/Factors/PostgresTotpEnrollmentSeeder.cs` - explicit bootstrap seed для `TOTP` enrollment
- `backend/OtpAuth.Infrastructure/Integrations/JwtIntegrationAccessTokenIssuer.cs` - JWT issuance/introspection с `kid` и validation по current + legacy signing keys
- `backend/OtpAuth.Api/Authentication/IntegrationClientContextHttpContextExtensions.cs` - сбор integration client context из JWT claims
- `backend/scripts/initialize-postgres.ps1` - bootstrap скрипт для `ensure-database + migrate + optional seed`
- `backend/scripts/maintain-security-data.ps1` - maintenance-скрипт для `TOTP` re-encryption и cleanup security-данных
- `backend/OtpAuth.Migrations/Migrations/202604140004_CreateTotpUsedTimeSteps.cs` - схема anti-replay reservation для `TOTP`
- `backend/OtpAuth.Migrations/Migrations/202604140005_CreateRevokedIntegrationAccessTokens.cs` - схема revoked integration access token store
- `backend/OtpAuth.Infrastructure/Persistence/SecurityDataCleanupService.cs` - retention cleanup для `challenge_attempts`, `totp_used_time_steps` и `revoked_integration_access_tokens`

## Последнее обновление

- `2026-04-14`: карта создана и привязана к scaffold-корням mobile, backend, admin, infra и MCP
- `2026-04-14`: канонические корни переключены с `src/*` на `mobile`, `backend`, `admin`
- `2026-04-14`: legacy-дубли и generated/cache хвосты удалены из рабочего дерева
- `2026-04-14`: карта дополнена веткой `Security` и ссылкой на дальнейшую детализацию backend-модулей
- `2026-04-14`: карта дополнена backend entry points для первого `Challenge` vertical slice и test entry point для `Challenge` handlers
- `2026-04-14`: карта дополнена entry point для `VerifyTotp` и bootstrap `TOTP` verifier
- `2026-04-14`: карта дополнена bootstrap integration auth layer и scoping entry points для `Challenges`
- `2026-04-14`: карта дополнена bootstrap OAuth entry points: `/oauth2/token`, in-memory client store и JWT issuer
- `2026-04-14`: карта дополнена `PostgreSQL` migration runner, persistent integration client store и bootstrap database script
- `2026-04-14`: карта дополнена `PostgreSQL`-backed challenge persistence и env-based bootstrap `TOTP` secret provider
- `2026-04-14`: карта дополнена encrypted `TOTP` enrollment storage, enrollment-backed verifier и `challenge_attempts`
- `2026-04-14`: карта дополнена persistent anti-replay/rate limiting для `VerifyTotp`, migration для `totp_used_time_steps` и обновленным bootstrap скриптом миграций
- `2026-04-14`: карта дополнена bootstrap OAuth introspection/revocation, runtime revoked-token validation и migration для `revoked_integration_access_tokens`
- `2026-04-14`: карта дополнена rotation-ready key rings для `TOTP` и bootstrap OAuth signing keys
- `2026-04-14`: карта дополнена maintenance workflow для `TOTP` re-encryption и cleanup/retention security-данных
