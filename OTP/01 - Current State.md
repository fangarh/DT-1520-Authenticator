# Current State

## Статус проекта

Проект находится на стадии архитектурного проектирования.

Реализовано сейчас:

- базовый Obsidian vault в `OTP/`
- архитектурные заметки по платформе `2FA/MFA`
- агентский протокол работы через vault
- верхнеуровневая структура папок для дальнейшей систематизации заметок
- доменные индексы по `Architecture`, `Data`, `Integrations`, `Product`, `Delivery`
- создана каноническая ветка `Security`
- черновой `ERD`
- черновой `OpenAPI v1`
- расширенный `OpenAPI v1` draft с `security`, `webhooks`, `Problem`-ошибками и idempotency
- auth/token модель для integration clients и mobile devices зафиксирована в vault и контракте
- `openapi-v1.yaml` прогнан через `Redocly CLI` и проходит валидацию
- утвержден `MVP` technology baseline
- зафиксировано, что `2FA Server` является первой фазой на пути к будущему `IdP`
- утвержден `Android-first` подход для мобильного фактора
- зафиксированы bootstrap-параметры `Android`-проекта
- утверждена новая корневая структура репозитория без верхнего `src/`
- создан Android scaffold в `mobile`
- создан backend scaffold в `backend`
- создан admin scaffold в `admin`
- создан installer UI scaffold в `installer-ui`
- добавлены локальные примеры `MCP`-конфигов в `config/mcp`
- `admin` проходит `npm run build`
- `backend` проходит `dotnet restore` и `dotnet build`
- план реализации на `8-12` недель
- для `backend` в текущем окружении зафиксирована последовательная solution-сборка, чтобы обойти `MSBuild` file locking в `artifacts/obj`
- для `mobile` подтверждено, что зависимости резолвятся при наличии корректного `JAVA_HOME`
- подготовлен backlog недостающей документации `P0/P1/P2`
- добавлены draft-заметки по `Backend Module Design`, `Security Model MVP`, `Testing Strategy` и плану первой недели
- добавлен draft-документ `Policy Design` как рабочий контракт для `Policy` модуля
- утверждены `ADR` по tenancy, enrollment и device trust для `MVP`
- зафиксировано, что `push` не является обязательной опорой для `on-prem` и future air-gapped профилей
- зафиксировано, что `Policy` обязателен уже в `MVP`, но как внутренний модуль, а не внешний engine
- добавлены backend contracts и `DefaultPolicyEvaluator` для `Policy`
- добавлен test project `OtpAuth.Infrastructure.Tests` с unit tests для policy evaluation
- `OtpAuth.Infrastructure.Tests` проходит `dotnet test`
- `OtpAuth.Api` проходит `dotnet build` после регистрации `Policy` evaluator
- добавлен `backend/scripts/verify-backend.ps1` для безопасной последовательной backend-проверки
- реализован первый backend use case `CreateChallenge` с вызовом `Policy`
- реализован read path для `Challenge`: `GetChallengeHandler` и `GET /api/v1/challenges/{id}`
- реализован `VerifyTotp` flow: application handler, `POST /api/v1/challenges/{id}/verify-totp`, state transitions `pending -> approved/failed/expired`
- реализован bootstrap `OAuth 2.0 client_credentials` flow: `POST /oauth2/token`, JWT issuance, scope enforcement и tenant/application scoping для `Challenges`
- реализованы bootstrap `OAuth 2.0` introspection и token revocation: `POST /oauth2/introspect`, `POST /oauth2/revoke`, persistent revoked-token store и runtime revocation enforcement
- реализована rotation-ready key management модель: `TOTP` protector поддерживает current + legacy keys по `key version`, а bootstrap JWT issuer поддерживает current + legacy signing keys по `kid`
- добавлен `PostgreSQL` migration runner `OtpAuth.Migrations` на `FluentMigrator`
- реализован `PostgreSQL`-backed `IntegrationClient` storage на `Dapper + Mapperly`
- реализован `PostgreSQL`-backed `Challenge` storage на `Dapper + Mapperly`
- реализованы encrypted `TOTP` enrollment storage и append-only `challenge_attempts`
- реализованы persistent `TOTP` anti-replay и runtime rate limiting для `VerifyTotp`
- на предоставленном сервере создана БД `dt-auth` и применена начальная схема `auth.integration_clients + auth.integration_client_scopes`
- на предоставленном сервере применена схема `auth.challenges`; `CreateChallenge -> GetChallenge -> VerifyTotp` подтвержден end-to-end против реального `PostgreSQL`
- на предоставленном сервере применены схемы `auth.totp_enrollments` и `auth.challenge_attempts`; `verify-totp` подтвержден против enrollment secret из БД
- на предоставленном сервере применена схема `auth.totp_used_time_steps`; replay и rate limiting подтверждены end-to-end против реального `PostgreSQL`
- bootstrap integration client seeded в `dt-auth`, `/oauth2/token` проверен end-to-end на реальном `PostgreSQL`
- bootstrap revoke/introspect проверены end-to-end на реальном `PostgreSQL`; revoked bearer token получает `401` на защищенных endpoint-ах
- rotation-ready `TOTP` verification подтвержден against real `dt-auth`: enrollment, зашифрованный старым ключом, проходит verify при новом current key и подключенном legacy key
- реализованы operational maintenance workflows: automated `TOTP` secrets re-encryption и cleanup/retention для `challenge_attempts`, `totp_used_time_steps`, `revoked_integration_access_tokens`
- на предоставленном сервере выполнен `TOTP` re-encryption workflow: enrollment переведен с `key_version=1` на current `key_version=2`
- на предоставленном сервере выполнен security cleanup workflow: удалены истекшие anti-replay reservations
- rotation-ready `TOTP` verification повторно подтвержден against real `dt-auth` уже без legacy `TOTP` key в runtime-конфигурации
- реализован append-only audit/reporting для `TOTP` protection key lifecycle: sanitized snapshot-ы usage по `key_version` пишутся в `auth.totp_protection_key_audit_events`
- реализован operational lifecycle integration clients: rotate secret, deactivate, reactivate через migration runner и отдельный script
- integration client auth state теперь хранится в `PostgreSQL` через `last_auth_state_changed_utc`, а bootstrap JWT валидируется с учетом `iat >= last_auth_state_changed_utc`
- реализован operational signing key lifecycle: legacy signing keys получают `RetireAtUtc`, runtime validation fail-closed отклоняет retired `kid`, а inspection script показывает rollout/retirement status
- реализован append-only audit/reporting для signing key lifecycle: sanitized snapshot-ы key ring пишутся в `auth.signing_key_audit_events`, а reporting читает последние audit events без доступа к signing material
- на предоставленном сервере `dt-auth` применена миграция `auth.integration_clients.last_auth_state_changed_utc`
- на предоставленном сервере `dt-auth` применены схемы `auth.signing_key_audit_events` и `auth.totp_protection_key_audit_events`; записаны первые sanitized audit snapshot-ы lifecycle ключей
- реализован единый append-only security audit contour: `auth.security_audit_events` backfill-ит legacy signing/`TOTP` lifecycle snapshot-ы и принимает новые sanitized events для signing key lifecycle, `TOTP` protection key lifecycle и integration client lifecycle
- на предоставленном сервере `dt-auth` применена миграция `auth.security_audit_events`; подтверждены новые события `signing_key_lifecycle.snapshot` и `integration_client_lifecycle.state_change_skipped`
- реализован admin/trusted-integration `TOTP` enrollment API: `POST /api/v1/enrollments/totp` и `POST /api/v1/enrollments/totp/{id}/confirm`
- реализован read path для `TOTP` enrollment: `GET /api/v1/enrollments/totp/{id}` без повторной выдачи provisioning artifacts
- реализован revoke flow для `TOTP` enrollment: `POST /api/v1/enrollments/totp/{id}/revoke` переводит enrollment в `revoked` и исключает его из active verify-path
- реализован safe replace flow для `TOTP` enrollment: `POST /api/v1/enrollments/totp/{id}/replace` запускает replacement artifact, не отключая текущий активный фактор до успешного confirm нового секрета
- enrollment management API теперь покрыт endpoint-level tests для `401/403/409`, scope enforcement, `Location` у start flow, safe replacement confirm-path и отсутствия provisioning artifacts вне `start/replace`
- enrollment lifecycle теперь пишет sanitized append-only events в `auth.security_audit_events`: `totp_enrollment.started`, `totp_enrollment.confirmed`, `totp_enrollment.confirmation_failed` и `totp_enrollment.confirmation_locked`
- enrollment lifecycle теперь также пишет `totp_enrollment.revoked` в unified append-only security audit trail
- enrollment lifecycle теперь также пишет replacement-события `totp_enrollment.replacement_started`, `totp_enrollment.replacement_confirmed`, `totp_enrollment.replacement_confirmation_failed` и `totp_enrollment.replacement_locked`
- `TOTP` enrollment confirmation защищен от brute-force через persisted `failed_confirm_attempts` и принудительный restart enrollment после лимита неудачных confirm-попыток
- replacement confirm-path защищен отдельным persisted счетчиком `replacement_failed_confirm_attempts`, не влияющим на текущий активный verify-path
- реализован bootstrap `admin auth contour` для `Admin UI MVP`: `POST /api/v1/admin/auth/login`, `POST /api/v1/admin/auth/logout`, `GET /api/v1/admin/auth/session`, `GET /api/v1/admin/auth/csrf-token`, cookie-based session, `CSRF`, login rate limiting и sanitized audit событий `admin_auth.*`
- добавлены `PostgreSQL`-схемы `auth.admin_users` и `auth.admin_user_permissions` для bootstrap operator credential/permission store
- реализован current enrollment admin read model по `tenantId + externalUserId`: `GET /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/enrollments/totp/current` возвращает sanitized current summary без provisioning artifacts
- в `auth.totp_enrollments` добавлен `revoked_utc`, чтобы admin/operator read model мог безопасно отдавать фактическое время revoke вместо косвенных derived timestamps
- реализован admin-facing enrollment command transport: `POST /api/v1/admin/enrollments/totp`, `POST /api/v1/admin/enrollments/totp/{enrollmentId}/confirm`, `POST /api/v1/admin/enrollments/totp/{enrollmentId}/replace`, `POST /api/v1/admin/enrollments/totp/{enrollmentId}/revoke`
- admin `start` теперь использует явный `applicationClientId` или fail-closed auto-resolve только при единственном активном integration client-е tenant-а; `confirm/replace/revoke` используют отдельный admin lookup по `enrollmentId`
- admin enrollment actions теперь пишут append-only sanitized audit события `admin_totp_enrollment.*` с привязкой к `adminUserId`, а state-changing endpoints под cookie session требуют `CSRF`
- реализован frontend `Admin UI MVP` shell в `admin`: session bootstrap, login/logout, lookup текущего enrollment-а, `start/confirm/replace/revoke`, problem mapping и одноразовое отображение provisioning artifacts только в рамках текущей operator session
- `backend` проходит последовательную проверку `build + tests` с `174` automated tests
- `admin` проходит `npm test` (`19` tests), `npm run build` и `npm run test:e2e`; `Vitest` настроен на `jsdom`, а checked-in `Playwright` suite прогоняет scripted browser regression для `login/logout`, `start/confirm`, `reload/load current`, `replace/confirm`, `revoke` и проверки, что provisioning artifact не живет в `localStorage`
- добавлены operational bootstrap-команды `list-admin-users` и `upsert-admin-user` в `OtpAuth.Migrations`; bootstrap admin password читается только из `OTPAUTH_ADMIN_PASSWORD`, permissions ограничены whitelist-ом `enrollments.read|write`
- live browser verification через `Playwright MCP` теперь подтверждена на реальном backend/runtime contour: `logout/login`, `start -> confirm`, `replace -> confirm`, `revoke` и follow-up `Load current` успешно пройдены против `OtpAuth.Api` на `127.0.0.1:5112` и `dt-auth`
- для live browser verification на `2026-04-15` применены актуальные миграции к `dt-auth`, создан bootstrap admin user `operator`, а единственный оставшийся console noise в UI — косметический `404` на `favicon.ico`
- зафиксирован `Installer MVP` design contract для `Docker Compose`-based `on-prem` bootstrap: preflight, secret boundary, idempotent bootstrap sequence и handoff в runtime `Admin UI`
- реализован первый packaging slice для `Installer MVP` в `infra/`: `Dockerfile` для `api/worker/admin/bootstrap`, `docker-compose.yml`, `HTTPS` edge для `Admin UI`, env contract example и automated packaging contract checks
- реализован первый installer slice поверх packaging: `infra/scripts/install.ps1` с preflight, bootstrap orchestration и runtime startup, плюс unit-style tests для installer helpers
- installer contour расширен до режимов `install/update/recover`; добавлен baseline operational runbook и sanitized runtime status report через `docker compose ps`
- `OtpAuth.Worker` теперь публикует sanitized execution snapshot в `heartbeat.json`: compose healthcheck использует его как liveness signal, а installer/recovery runbook получил worker-specific troubleshooting по dependency probes `Postgres/Redis`
- `OtpAuth.Worker` теперь имеет первый реальный background job `security_data_cleanup`: heartbeat snapshot дополнительно публикует `jobStatuses` с последним run, `blocked/degraded/healthy` статусом и sanitized progress metrics по cleanup
- принят `ADR-029`: installer развивается по модели `script-first, UI-second`; source of truth остается в `infra/scripts/*`, а будущий installer UI допускается только как отдельный локальный setup surface вне runtime `admin/`
- завершена `Iteration 1` по installer engine hardening: добавлены `infra/scripts/Installer.Contract.ps1`, structured validation issues, stable step ids/results и sanitized JSON-report через `install.ps1 -ReportJsonPath`
- завершена `Iteration 2` по installer recovery/diagnostics hardening: добавлен `infra/scripts/Installer.Diagnostics.ps1`, `worker` в `install/update/recover` теперь стартует через `--wait`, а installer report включает structured runtime service status, sanitized worker heartbeat snapshot и troubleshooting hints для partial failure без утечки секретов
- завершена `Iteration 3` по installer UI shell: добавлен отдельный loopback-only `installer-ui` поверх `install.ps1`, локальный Node bridge не хранит bootstrap password вне памяти процесса, а UI показывает только sanitized manifest/report contract
- `installer-ui` проходит `npm test` (`5` tests), `npm run build` и `npm run test:e2e` на mock bridge; browser flow подтвержден через `Playwright`
- завершена `Iteration 4` по installer UI completion: `installer-ui` теперь знает `OperationProfile/Manifest/Configuration` из engine report, добавляет mode-aware guardrails для live `Install` path, ведет оператора через handoff/next steps для `install/update/recover` и закрывает happy path до runtime `Admin UI`
- `installer-ui` после Iteration 4 проходит `npm test` (`7` tests), `npm run build` и `npm run test:e2e`; из текущей среды `Playwright` e2e потребовал запуск вне sandbox из-за локального `EPERM`
- зафиксирован отдельный рабочий план `Android TOTP-first` с checkpoint-ами для промежуточной очистки контекста
- `mobile` переведен с single-module scaffold на clean multi-module layout: `app`, `core:ui`, `feature:provisioning`, `feature:totp-codes`, `security:storage`, `totp-domain`
- в `mobile/security:storage` реализован secure storage abstraction для `TOTP-first`: `SecureTotpSecretStore`, `SharedPreferences + Android Keystore AES/GCM`, hashed storage keys и fail-closed restore path
- в `mobile/totp-domain` реализован `Step 4` из `Android TOTP-first`: `otpauth://` parser, strict validation `issuer/label/secret/digits/period/algorithm`, RFC-backed `TOTP` code generation и countdown/state model без Android-зависимостей
- в `mobile/feature:provisioning` реализован `Step 5` из `Android TOTP-first`: masked `otpauth://` import, manual fallback, preview/confirm/save flow и app wiring в secure storage без прямой зависимости feature-модуля от `Keystore`
- в `mobile/feature:totp-codes` реализован `Step 6` из `Android TOTP-first`: runtime list of saved accounts, offline `TOTP` code + countdown, explicit remove confirm flow и scroll-safe app shell для реального Android viewport
- завершен `Step 7` из `Android TOTP-first`: stable provisioning contract зафиксирован в vault, `OpenAPI` явно описывает artifact-only поля `secretUri/qrCodePayload`, а mobile/admin/security notes синхронизированы с backend contract

Пока не реализовано:

- production-ready backend за пределами bootstrap `Challenges` slice
- mobile app
- для закрытия текущего `Android TOTP-first` slice все еще нужны базовые `Compose` UI/instrumented tests для provisioning/runtime screens, повторный live verification contour и финальное DoD closure в vault
- `API`
- отдельный `Bootstrap Agent`
- полноценное integration/contract/e2e покрытие за пределами текущего bootstrap backend slice
- bootstrap OAuth теперь читает integration clients из `PostgreSQL`; operational secret rotation, client activation lifecycle и единый security audit trail уже реализованы, но полноценный management API/UI еще не готовы
- без конфигурации signing key bootstrap OAuth по-прежнему может использовать process-local ephemeral key, но теперь это разрешено только в `Development`
- `TOTP` enrollment storage уже persistent и шифруется at rest, но пока опирается на env-managed protection key, а не на `Vault/KMS`
- `TOTP` enrollment management backend и runtime operator shell для `Admin UI MVP` уже закрывают `start/confirm/status/revoke/replace`; live browser verification через `Playwright` подтверждена, а checked-in scripted regression suite теперь отдельно оформлена в `admin/e2e`
- bootstrap runtime теперь имеет revoke/introspect, key-ring-based validation, automated `TOTP` re-encryption, token/attempt cleanup, operational integration client lifecycle, config-driven signing key rollout/retirement и единый append-only security audit trail, но все еще не имеет signing key lifecycle management UI/API и полноценного admin/API client management
- `Installer MVP` по локальному `Docker Compose` path теперь закрывает end-to-end operator happy path через `installer-ui`, но более широкий `bootstrap/setup plane` все еще не завершен из-за отсутствия отдельного `Bootstrap Agent`
- для `Admin UI MVP` фронт и backend уже используют отдельный `admin auth contour`, current enrollment read model по `tenantId + externalUserId` и admin command transport; `Playwright MCP` после restart больше не блокирует live browser verification, а пройденный flow подтверждает рабочий runtime contour end-to-end

## Текущая продуктовая рамка

Целевой продукт первой фазы: отдельный `2FA Server`, подключаемый к существующим системам.

Стратегическая цель продукта: эволюция к полноценному `IdP`.

Базовые факторы первой очереди:

- `TOTP`
- `Push approval`
- `Backup codes`

## Текущая архитектурная позиция

- стартовать с `REST-first` интеграции
- первый релиз строить как `modular monolith`
- использовать `ASP.NET Core + PostgreSQL + Redis + Outbox/Worker`
- не использовать `Entity Framework`; persistence строить на `Dapper`, object mapping на `Mapperly`
- schema migrations вести через отдельный `FluentMigrator` runner
- mobile factor первой версии делать как `Android` app на `Kotlin`
- `iPhone` держать вне ближайшего delivery scope и рассматривать только после стабилизации `Android` и backend
- делать `Policy` отдельным модулем внутри монолита с code-first правилами и `deny by default`
- проектировать с учетом будущих `OIDC`, `SAML`, `RADIUS`, `LDAP/AD`
- поддерживать облачный и коробочный режимы из одной кодовой базы
- отделять `bootstrap/setup plane` от runtime `Admin UI`; сначала фиксировать setup architecture, затем делать `Admin UI`, и только потом `Installer MVP`
- развивать installer как `script-first` engine с возможным отдельным локальным UI поверх него, а не как часть runtime `admin/`
- отделять human-operator `Admin UI` auth contour от integration `client_credentials` contour
- строить operator enrollment lookup по `tenantId + externalUserId`, а не вокруг одного `enrollmentId`

## Практический смысл этого файла

Этот файл должен быстро отвечать на два вопроса:

1. что уже реально существует в репозитории
2. в какой стадии находится продукт и его реализация

## Рабочий статус scaffold-ов

- `mobile` существует как multi-module Android workspace и готов к следующему `TOTP-first` slice
- `admin` существует, зависимости установлены, build проходит
- `admin` имеет runtime shell поверх `/api/v1/admin/*`, helper/API/component tests и локальный `Vite` proxy на backend
- `installer-ui` существует как отдельный local setup shell: loopback-only bridge, typed React/Vite UI, unit tests и checked-in `Playwright` regression на mock bridge
- `installer-ui` теперь также закрывает operational closure: mode-specific guidance, client-side guardrails, sanitized handoff в runtime `Admin UI` и runbook-aligned next steps
- `backend` существует, restore/build проходят
- `mobile` теперь имеет канонический multi-module layout: `:app`, `:core:ui`, `:feature:provisioning`, `:feature:totp-codes`, `:security:storage`, `:totp-domain`
- `mobile/security:storage` теперь уже закрывает `Step 3` из `Android TOTP-first`: Android store поверх `Keystore`, pure-Kotlin serialization/persistence helpers и unit tests на validation/restore/error paths
- `mobile/totp-domain` теперь уже закрывает `Step 4` из `Android TOTP-first`: provisioning parser, redacted secret-bearing value objects, RFC test vectors для `SHA1/SHA256/SHA512` и pure-Kotlin countdown model
- `mobile/feature:provisioning` теперь уже закрывает `Step 5` из `Android TOTP-first`: URI/manual import flow, safe preview without secret echo, secure save в `Android Keystore`-backed store и unit tests на invalid/valid import path
- `mobile/security:storage` теперь хранит не только `account + secret`, но и `digits + algorithm`, чтобы provisioning flow не терял `TOTP` metadata при persistence
- `infra` содержит первый runtime packaging slice для `on-prem`: `docker-compose`, `Dockerfile`, env contract example и packaging contract checks
- `infra` также содержит первый installer entry point: `install.ps1`, operation profiles `install/update/recover`, manifest/report contract, preflight helpers и unit-style tests для install plan/config validation
- `infra` теперь также содержит structured diagnostics layer: `Installer.Diagnostics.ps1`, JSON-report с runtime service status, worker heartbeat snapshot и troubleshooting hints, а `worker` startup в installer идет через `--wait`
- `infra` теперь также имеет compose healthcheck для `worker` на основе execution snapshot freshness, а `backend/OtpAuth.Worker.Tests` покрывает publisher/coordinator/job diagnostic slice unit tests
- `backend` solution build в этом workspace стабилизирован через отключение параллельной сборки на уровне solution props
- для backend restore/build в этой среде потребовался запуск вне sandbox
- `mobile` требует настроенный `JAVA_HOME` в shell или IDE; локальный `Android OpenJDK` на машине доступен
- старый верхний `src/`, внутренний `backend/src/` и generated-cache хвосты удалены
- в рабочем дереве остались только канонические корни и исходные scaffold-файлы

## Текущее расположение архитектурных заметок

Канонические заметки по архитектуре и смежным доменам теперь находятся в:

- `OTP/Architecture/`
- `OTP/Data/`
- `OTP/Integrations/`
- `OTP/Product/`
- `OTP/Delivery/`
- `OTP/Security/`

Исторические заметки-снимки по `2FA/MFA` сохранены в `OTP/2FA/`.

## Последнее обновление

- `2026-04-14`: создана базовая структура knowledge vault и протокол `vault-first`
- `2026-04-14`: добавлены канонические доменные заметки, `ERD`, `OpenAPI v1` и план реализации
- `2026-04-14`: `OpenAPI v1` усилен до более строгого draft-контракта
- `2026-04-14`: в `OpenAPI v1` добавлены auth/token flow, callbacks и production-style headers/examples
- `2026-04-14`: `openapi-v1.yaml` успешно провалидирован через `Redocly CLI`
- `2026-04-14`: утвержден technology baseline и `Android-first` mobile approach
- `2026-04-14`: зафиксированы параметры bootstrap для Android Studio проекта
- `2026-04-14`: созданы scaffold-проекты для mobile/backend/admin и локальные примеры `MCP`
- `2026-04-14`: backend scaffold успешно восстановлен и собран
- `2026-04-14`: утверждена и применена новая корневая структура репозитория без верхнего `src/`
- `2026-04-14`: удалены legacy-дубли и локальные generated/cache артефакты после миграции структуры
- `2026-04-14`: после переноса workspace повторно проверены зависимости `admin/backend/mobile`; для backend зафиксирована последовательная solution-сборка, для mobile подтвержден `Gradle` resolve при корректном `JAVA_HOME`
- `2026-04-14`: добавлена security-ветка vault и подготовлены draft-заметки для `Backend Module Design`, `Security Model MVP`, `Testing Strategy`, `Documentation Backlog` и `Week 1 Execution Plan`
- `2026-04-14`: добавлен `Policy Design` с `PolicyContext`, `PolicyDecision`, `MVP` ruleset и backend mapping
- `2026-04-14`: приняты `ADR-011`, `ADR-012` и `ADR-013` по tenancy model, enrollment model и device trust lifecycle
- `2026-04-14`: приняты `ADR-014` и `ADR-015` по стратегической цели `IdP` и optional `push` для `on-prem`/future air-gapped профилей
- `2026-04-14`: принят `ADR-016` по `Policy` как внутреннему модулю `MVP`, а не внешнему engine
- `2026-04-14`: в backend добавлены `Policy` enums, contracts, `DefaultPolicyEvaluator`, DI registration и unit tests в `OtpAuth.Infrastructure.Tests`
- `2026-04-14`: `OtpAuth.Infrastructure.Tests` успешно проходит `dotnet test`, `OtpAuth.Api` успешно проходит `dotnet build`
- `2026-04-14`: добавлен `backend/scripts/verify-backend.ps1`; backend `build/test` теперь должен запускаться последовательно, а не параллельно
- `2026-04-14`: реализован первый `CreateChallenge` vertical slice: domain/application model, in-memory repository, API endpoint и unit tests
- `2026-04-14`: первый `Challenge` slice доведен до read path: добавлены `GetChallengeHandler`, `GET /api/v1/challenges/{id}` и дополнительные unit tests; последовательная backend-проверка проходит с `12/12`
- `2026-04-14`: добавлен `VerifyTotp` flow с bootstrap `TOTP` verifier, одноразовым verify-path и переходами `approved/failed/expired`; последовательная backend-проверка проходит с `18/18`
- `2026-04-14`: реализован bootstrap `OAuth 2.0 client_credentials` flow с `/oauth2/token`, JWT bearer validation, scope enforcement и tenant/application scoping для `Challenges`; последовательная backend-проверка проходит с `33/33`
- `2026-04-14`: добавлены `FluentMigrator`-based migration runner, `PostgreSQL`-backed `IntegrationClient` storage на `Dapper + Mapperly`, скрипт `initialize-postgres.ps1`; на сервере создана БД `dt-auth` и применена начальная схема, backend-проверка проходит с `38/38`
- `2026-04-14`: bootstrap integration client seeded в `dt-auth`; `/oauth2/token` успешно проверен end-to-end против реального `PostgreSQL`
- `2026-04-14`: `Challenges` переведены на `PostgreSQL`-backed persistence; схема `auth.challenges` применена, а flow `CreateChallenge -> GetChallenge -> VerifyTotp` успешно проверен end-to-end против `dt-auth`; backend-проверка проходит с `44/44`
- `2026-04-14`: добавлены encrypted `TOTP` enrollment storage и `challenge_attempts`; `verify-totp` переведен на enrollment-backed secret из `PostgreSQL`, а flow подтвержден end-to-end против `dt-auth`; backend-проверка проходит с `52/52`
- `2026-04-14`: добавлены persistent `TOTP` anti-replay и runtime rate limiting через `auth.totp_used_time_steps` и `auth.challenge_attempts`, `VerifyTotp` возвращает `429` с `Retry-After`, migration runner стабилизирован через явный `build -> run --no-build`, backend-проверка проходит с `59/59`
- `2026-04-14`: добавлены bootstrap `OAuth` token revocation и introspection с persistent store `auth.revoked_integration_access_tokens` и runtime enforcement revoked bearer tokens; backend-проверка проходит с `71/71`
- `2026-04-14`: добавлена rotation-ready key management модель для `TOTP` и bootstrap OAuth: current + legacy keys, JWT `kid`, validation по key ring; backend-проверка проходит с `75/75`
- `2026-04-14`: добавлены operational workflows для `TOTP` re-encryption и security cleanup/retention, выполнен re-encryption enrollment-а в `dt-auth` на `key_version=2`, cleanup удалил истекшие anti-replay reservations, а runtime verify повторно подтвержден без legacy `TOTP` key; backend-проверка проходит с `79/79`
- `2026-04-14`: добавлен operational lifecycle для integration clients: rotate secret, deactivate/reactivate, persisted `last_auth_state_changed_utc`, runtime/introspection invalidation issued JWT через `iat`; миграция применена к `dt-auth`, backend-проверка проходит с `86/86`
- `2026-04-14`: добавлен operational signing key lifecycle: `RetireAtUtc` для legacy signing keys, fail-closed validation retired `kid`, inspection command/script и запрет ephemeral signing key вне `Development`; backend-проверка проходит с `89/89`
- `2026-04-14`: добавлен append-only audit/reporting для signing key lifecycle: таблица `auth.signing_key_audit_events`, команды audit/list, script `audit-signing-key-lifecycle.ps1`; backend-проверка проходит с `92/92`
- `2026-04-14`: добавлен append-only audit/reporting для `TOTP` protection key lifecycle: таблица `auth.totp_protection_key_audit_events`, команды inspect/audit/list и script `audit-totp-protection-key-lifecycle.ps1`; миграции и первый audit snapshot применены к `dt-auth`, backend-проверка проходит с `94/94`
- `2026-04-15`: добавлен единый security audit contour: `auth.security_audit_events`, generic `SecurityAuditService`, backfill legacy lifecycle snapshots, sanitized audit для integration client lifecycle и reporting-команда `list-security-audit-events`; миграция применена к `dt-auth`, backend-проверка проходит с `99/99`
- `2026-04-15`: реализован `TOTP` enrollment slice: `POST /api/v1/enrollments/totp`, `POST /api/v1/enrollments/totp/{id}/confirm`, persisted confirm-attempt tracking через `failed_confirm_attempts`, sanitized enrollment lifecycle audit events в unified trail и unit coverage `110/110`
- `2026-04-15`: завершена Iteration 1 backend enrollment management: `GET /api/v1/enrollments/totp/{id}` возвращает scoped enrollment status без `secretUri/qrCodePayload`; backend verification проходит с `113/113`
- `2026-04-15`: завершена Iteration 2 backend enrollment management: `POST /api/v1/enrollments/totp/{id}/revoke` переводит enrollment в `revoked`, пишет `totp_enrollment.revoked` и проходит backend verification с `117/117`
- `2026-04-15`: завершена Iteration 3 backend enrollment management: `POST /api/v1/enrollments/totp/{id}/replace` создает replacement artifact без потери активного фактора, confirm replacement использует отдельный pending replacement state и verification проходит с `123/123`
- `2026-04-15`: завершена Iteration 4 backend enrollment management: добавлен endpoint-level hardening test harness для enrollment API (`401/403/409`, scope enforcement, `Location`, secret non-leakage, replacement confirm-path); backend verification проходит с `133/133`
- `2026-04-15`: принят `ADR-025`; для `on-prem` bootstrap/install контур отделен от runtime `Admin UI`, а рекомендованный порядок delivery зафиксирован как `setup plane design -> Admin UI -> Installer MVP`
- `2026-04-15`: добавлен `Admin UI MVP Plan`; scope ограничен runtime `TOTP` enrollment management, а перед UI implementation зафиксированы `P0`-вопросы по `admin auth contour` и enrollment lookup/read model
- `2026-04-15`: приняты `ADR-026` и `ADR-027`; `Admin UI` идет через отдельный admin auth contour, а operator UX опирается на current enrollment read model по `tenantId + externalUserId`
- `2026-04-15`: добавлен `Admin Backend Scope for Admin UI MVP`; backend-декомпозиция зафиксирована по slices: `admin auth`, admin read model, admin command transport, audit и hardening
- `2026-04-15`: завершена Iteration 1 `Admin UI MVP` backend foundation: добавлены `auth.admin_users`, cookie-based `admin auth contour`, `CSRF`, login rate limit, admin session endpoints и `147/147` automated tests
- `2026-04-15`: завершена Iteration 2 `Admin UI MVP` backend read model: добавлены `GET /api/v1/admin/tenants/{tenantId}/users/{externalUserId}/enrollments/totp/current`, current enrollment lookup без provisioning artifacts, `auth.totp_enrollments.revoked_utc` и `154/154` automated tests
- `2026-04-15`: принят `ADR-028`; admin `start` использует явный `applicationClientId` или auto-resolve только при единственном активном integration client-е tenant-а, а admin command transport не требует integration auth context
- `2026-04-15`: завершена Iteration 3 `Admin UI MVP` backend command transport: добавлены admin endpoints для `start/confirm/replace/revoke`, отдельный by-id admin lookup, `admin_totp_enrollment.*` audit и `167/167` automated tests
- `2026-04-15`: собран frontend `Admin UI MVP` shell в `admin`: `app`-shell, `features/auth`, enrollment workspace, API client для `/api/v1/admin/*`, problem mapping, transport-error hardening и discard логика provisioning artifacts; `npm test` проходит с `15/15`, `npm run build` зеленый
- `2026-04-15`: для `admin` добавлены component-level tests на `Vitest + jsdom + Testing Library`: login form, status card и operator action panels (`start/confirm/replace/revoke`)
- `2026-04-15`: по скриншоту браузера выявлен frontend bootstrap-loop на `GET /api/v1/admin/auth/session`; `useAdminSession` исправлен так, чтобы session bootstrap не повторялся после собственного error state update, и добавлен regression test на single-attempt failure path
- `2026-04-15`: визуальная browser-проверка `Admin UI` через `Playwright` повторно попытана на живом `Vite` dev server (`http://127.0.0.1:4173` отвечает `200`), но заблокирована внешним сбоем `MCP Playwright` transport (`Transport closed`)
- `2026-04-15`: для `admin` добавлен checked-in `Playwright` regression contour: `@playwright/test`, `playwright.config.ts`, scripted fixture для `/api/v1/admin/*`, e2e flow `login -> start -> confirm -> reload/load current -> replace -> confirm -> revoke -> logout`, unit tests для `TOTP` helper-а и локальная проверка `npm test`, `npm run build`, `npm run test:e2e`
- `2026-04-15`: добавлен `OTP/Delivery/Installer MVP Plan.md`; зафиксирован минимальный contract для `Docker Compose`-based installer: preflight checks, install-time secret boundary, reuse `OtpAuth.Migrations`, idempotent bootstrap и handoff в runtime `Admin UI`
- `2026-04-15`: реализован первый packaging slice в `infra/`: `docker-compose.yml`, `Dockerfile` для `api/worker/admin/bootstrap`, `HTTPS` nginx edge для `Admin UI`, example runtime env contract и automated packaging contract checks
- `2026-04-15`: реализован первый installer slice в `infra/scripts`: `install.ps1` выполняет preflight, bootstrap через `OtpAuth.Migrations` и runtime startup поверх `docker compose`; добавлены unit-style tests `infra/tests/installer.common.tests.ps1`
- `2026-04-15`: installer расширен до operation modes `Install|Update|Recover`; добавлены baseline runtime status report через `docker compose ps` и `OTP/Delivery/Installer Operations Runbook.md`
- `2026-04-15`: для `OtpAuth.Worker` execution snapshot расширен до domain-aware diagnostics: snapshot теперь хранит `executionOutcome`, `consecutiveFailureCount` и dependency probe results для `Postgres/Redis`; `OtpAuth.Worker.Tests` расширен до `8` tests, а backend verification проходит с `179/179`
- `2026-04-15`: для `OtpAuth.Worker` добавлен job-level diagnostics contour: snapshot теперь хранит `jobStatuses`, первый реальный background job `security_data_cleanup` и sanitized progress metrics по cleanup, `OtpAuth.Worker.Tests` расширен до `13` tests, а backend verification проходит с `184/184`
- `2026-04-15`: принят `ADR-029`; installer зафиксирован как `script-first` engine с возможным отдельным локальным setup UI поверх machine-readable engine-контракта, а дальнейшая delivery-декомпозиция разбита на несколько итераций с checkpoint-ами на очистку контекста
- `2026-04-15`: завершена `Iteration 1` installer-трека: выделен `Installer.Contract.ps1`, `install.ps1` теперь умеет формировать manifest и писать sanitized JSON-report, validation переведена на structured issues, а installer tests усилены проверкой secret non-leakage в report
- `2026-04-15`: завершена `Iteration 2` installer-трека: выделен `Installer.Diagnostics.ps1`, `install.ps1` теперь дожидается healthy `worker`, JSON-report содержит structured runtime status и sanitized worker heartbeat snapshot, а degraded runtime получает troubleshooting hints без раскрытия secret material
- `2026-04-15`: завершена `Iteration 3` installer-трека: добавлен отдельный `installer-ui` на `React + Vite` с loopback-only Node bridge к `infra/scripts/install.ps1`, очищением bootstrap password после запуска и checked-in browser regression на mock bridge
- `2026-04-15`: завершена `Iteration 4` installer-трека: `installer-ui` расширен mode-aware guardrails и operational closure поверх engine report (`OperationProfile/Manifest/Configuration`), а browser verification подтверждена через `npm run test:e2e` вне sandbox после локального `EPERM` внутри sandbox
- `2026-04-15`: добавлен `OTP/Product/Android TOTP-First Plan.md` как канонический mobile checkpoint для продолжения после context reset
- `2026-04-15`: `mobile` переведен на clean multi-module layout для `Android TOTP-first`: `:app`, `:core:ui`, `:feature:provisioning`, `:feature:totp-codes`, `:security:storage`, `:totp-domain`
- `2026-04-15`: завершен `Step 3` mobile-трека: в `security:storage` добавлены `SecureTotpSecretStore`, Android `Keystore`-backed implementation, hashed storage keys, fail-closed restore path и unit tests; mobile unit contour проходит
- `2026-04-17`: завершен `Step 4` mobile-трека: в `totp-domain` добавлены `otpauth://` parser, validation `issuer/label/secret/digits/period/algorithm`, redacted secret-bearing domain types, RFC-backed `TOTP` code generation, countdown/state model и unit tests; mobile unit contour проходит
- `2026-04-17`: завершен `Step 5` mobile-трека: в `feature:provisioning` добавлены masked import forms для `otpauth://` и manual fallback, preview/confirm/save flow, app wiring в secure storage и unit tests; `security:storage` snapshot расширен до `digits + algorithm`, а полный mobile contour проходит
- `2026-04-17`: завершен `Step 6` mobile-трека: `feature:totp-codes` теперь рендерит список сохраненных аккаунтов, текущий офлайн-код, countdown и explicit remove confirm flow; `AuthenticatorApp` больше не держит secret-bearing preview после save, а live verification подтверждена на `emulator-5554`
- `2026-04-17`: завершен `Step 7` mobile-трека: добавлена каноническая заметка `OTP/Integrations/TOTP Provisioning Contract.md`, artifact visibility для `secretUri/qrCodePayload` синхронизирована между `OpenAPI`, `Product`, `Delivery` и `Security`, а следующим незакрытым блоком для `Android TOTP-first` остаются базовые `Compose` UI/instrumented tests и финальное DoD closure
