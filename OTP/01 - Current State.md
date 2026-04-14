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
- `backend` проходит последовательную проверку `build + tests` с `79` unit tests

Пока не реализовано:

- production-ready backend за пределами bootstrap `Challenges` slice
- mobile app
- `API`
- инфраструктурные манифесты
- полноценное покрытие тестами за пределами стартового `Policy` модуля
- bootstrap OAuth теперь читает integration clients из `PostgreSQL`; seeded bootstrap client уже подтверждает выпуск токенов, но secret rotation и client lifecycle еще не реализованы полноценно
- без конфигурации signing key bootstrap OAuth по-прежнему выдает токены с process-local ephemeral key
- `TOTP` enrollment storage уже persistent и шифруется at rest, но пока опирается на env-managed protection key, а не на `Vault/KMS`
- enrollment пока создается bootstrap seed-командой; полноценный enrollment API и provisioning flow еще не реализованы
- bootstrap runtime теперь имеет revoke/introspect, key-ring-based validation, automated `TOTP` re-encryption и token/attempt cleanup, но все еще не имеет signing key lifecycle management UI/API и полноценного client lifecycle management

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

## Практический смысл этого файла

Этот файл должен быстро отвечать на два вопроса:

1. что уже реально существует в репозитории
2. в какой стадии находится продукт и его реализация

## Рабочий статус scaffold-ов

- `mobile` существует и готов к дальнейшей разработке
- `admin` существует, зависимости установлены, build проходит
- `backend` существует, restore/build проходят
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
