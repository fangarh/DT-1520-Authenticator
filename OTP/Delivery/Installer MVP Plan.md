# Installer MVP Plan

## Status

Accepted working guideline

## Цель

Зафиксировать минимальный реализуемый контракт для первого `Installer MVP` так, чтобы команда могла перейти от абстрактного `setup plane` к конкретной delivery-декомпозиции без смешения installer-логики с runtime `Admin UI`.

## Текущее состояние

На `2026-04-17` локальный `Installer MVP` для `Docker Compose` path уже закрыт end-to-end:

- runtime units `backend/OtpAuth.Api` и `backend/OtpAuth.Worker`
- admin runtime UI в `admin/`
- migration/bootstrap runner `backend/OtpAuth.Migrations`
- operational scripts в `backend/scripts/`
- отдельный runtime admin contour, уже отделенный от install-time операций

В репозитории уже есть необходимые installer/package артефакты:

- `infra/docker/api.Dockerfile`
- `infra/docker/worker.Dockerfile`
- `infra/docker/admin.Dockerfile`
- `infra/docker/bootstrap.Dockerfile`
- `infra/docker-compose.yml`
- `infra/env/runtime.env.example`
- `infra/tests/packaging.contract.tests.ps1`
- `infra/scripts/install.ps1`
- `infra/scripts/Installer.Common.ps1`
- `infra/scripts/Installer.Diagnostics.ps1`
- `infra/tests/installer.common.tests.ps1`
- `installer-ui/`
- `OTP/Delivery/Installer Operations Runbook.md`

Что остается за пределами этого `Installer MVP`:

- отдельный `Bootstrap Agent` или иной более широкий `setup plane`
- следующий delivery track вокруг host-level lifecycle beyond текущего local installer shell

## Основания

- [[../Decisions/ADR-025 - Bootstrap Setup Plane Is Separate from Runtime Admin]]
- [[On-Prem Delivery]]
- [[../Architecture/Bootstrap Installer Plane]]

## Базовая форма поставки для `MVP`

Первый `Installer MVP` ориентируется на локальную или `on-prem` поставку через `Docker Compose`.

Минимальный runtime contour:

- `api`
- `worker`
- `admin`
- `postgres`
- `redis`

Допущение для первой итерации:

- `PostgreSQL` и `Redis` допускаются как compose-managed зависимости
- enterprise-вариант с внешними managed services, `Helm` и `Vault/KMS` остается отдельным последующим треком

## Позиция по installer UI

На `2026-04-15` принято направление `script-first, UI-second`.

- канонический installer engine живет в `infra/scripts/install.ps1` и `infra/scripts/Installer.Common.ps1`
- installer UI разрешен и полезен как follow-up для operator UX
- installer UI не должен встраиваться в runtime `admin/`
- installer UI должен быть отдельным локальным wizard/setup surface поверх того же engine-контракта
- до UI engine должен отдать machine-readable validation/status contract, иначе появятся два расходящихся orchestration path

## Что installer должен делать

### 1. Выполнять preflight checks

- проверять доступность `docker` и `docker compose`
- проверять, что целевая машина поддерживает выбранный deployment profile
- проверять доступность обязательных портов и writable runtime directory
- fail-closed останавливаться до записи конфигурации, если базовые зависимости не готовы

### 2. Принимать install-time конфигурацию

Минимальный набор install-time параметров должен покрывать уже существующие backend contracts:

- `ConnectionStrings__Postgres`
- `BootstrapOAuth__CurrentSigningKeyId`
- `BootstrapOAuth__CurrentSigningKey`
- `BootstrapOAuth__AdditionalSigningKeys__{n}__KeyId|Key|RetireAtUtc`, если rollout идет не с нуля
- `TotpProtection__CurrentKeyVersion`
- `TotpProtection__CurrentKey`
- `TotpProtection__AdditionalKeys__{n}__KeyVersion|Key`, если есть legacy ciphertext
- bootstrap admin username и permissions
- bootstrap admin password только через одноразовый secret input, совместимый с `OTPAUTH_ADMIN_PASSWORD`

Опора на текущие contracts уже есть в:

- `backend/README.md`
- `backend/OtpAuth.Api/Program.cs`
- `backend/OtpAuth.Migrations/Program.cs`

### 3. Сохранять runtime configuration вне репозитория

Installer должен писать runtime config в host-level protected location, а не в git-tracked файлы.

Для `MVP` допускается:

- локальный secrets/env file вне репозитория
- platform secret store для `Docker Compose`, если он доступен

Недопустимо:

- писать секреты в `OTP/`
- писать секреты в checked-in `appsettings*.json`
- логировать открытые значения signing keys, `TOTP` keys и admin passwords

### 4. Выполнять bootstrap-команды

Минимальная последовательность:

1. `ensure-database`
2. `migrate`
3. optional explicit seed operations
4. `upsert-admin-user`

Bootstrap-команды должны вызываться через уже существующий `backend/OtpAuth.Migrations`, а не дублироваться в новом installer-коде.

### 5. Поднимать runtime units

После успешного bootstrap installer:

- поднимает compose stack
- дожидается health-check или эквивалентного operational readiness сигнала
- возвращает оператору только sanitized install result

### 6. Передавать управление runtime `Admin UI`

Installer заканчивает работу после того, как runtime contour поднят и оператор может перейти в обычный `Admin UI`.

Installer не должен оставаться постоянной internet-facing control plane.

## Что installer не должен делать

- не должен использовать runtime `Admin UI` как оболочку для host-level команд
- не должен проксировать секреты через обычный публичный `OtpAuth.Api`
- не должен автоматически выполнять destructive maintenance операции
- не должен хранить bootstrap password дольше, чем нужно для `upsert-admin-user`
- не должен выполнять неявный reseed данных при повторном запуске

## Security boundary

### Обязательные правила

- setup flow доступен только локально или через выделенный защищенный операционный канал
- все secret inputs маскируются в UI, CLI output и логах
- bootstrap admin password не передается в CLI args и не записывается в audit payload
- после завершения установки setup surface должен быть отключаемым или ограниченным локальным доступом
- runtime `Admin UI` продолжает жить в отдельном auth contour и не получает installer privileges

### Аудит

Installer должен оставлять только sanitized operational trail:

- когда началась установка
- какой deployment profile выбран
- какие шаги завершились успешно или неуспешно
- без secret material и без connection string в открытом виде

## Idempotency contract

Повторный запуск installer на той же инсталляции должен вести себя предсказуемо:

- повторный preflight допустим и безопасен
- `ensure-database` и `migrate` должны оставаться idempotent
- `upsert-admin-user` должен обновлять или подтверждать bootstrap admin, а не создавать дубликаты
- уже сохраненная runtime config не должна без явного подтверждения перетираться новыми значениями
- optional seed steps должны быть явными флагами, а не silent default behavior

## Рекомендуемая декомпозиция реализации

### Slice A. Packaging

Status на `2026-04-15`: базовый runtime packaging slice реализован.

- `Dockerfile` для `api`
- `Dockerfile` для `worker`
- production packaging для `admin`
- базовый `docker-compose.yml`
- optional `bootstrap` image/profile поверх `OtpAuth.Migrations`
- example runtime env contract и packaging contract checks

### Slice B. Installer contract

Status на `2026-04-15`: `Iteration 1` завершена.

- формат install manifest или equivalent runtime config template
- machine-readable manifest contract уже выделен в `infra/scripts/Installer.Contract.ps1`
- structured validation errors уже реализованы
- structured step results и sanitized JSON-report уже реализованы
- secret input model
- runtime directory layout вне repo
- fail-closed rule: env file должен жить вне репозитория
- process-level secret handoff для `OTPAUTH_ADMIN_PASSWORD`

### Slice C. Bootstrap execution

Status на `2026-04-15`: начальная orchestration реализована в `infra/scripts/install.ps1`, включая `install/update/recover` modes.

- orchestration вокруг `OtpAuth.Migrations`
- создание или обновление bootstrap admin user
- install report без secret leakage
- update/recovery path без повторного использования runtime `Admin UI` как control plane

### Slice D. Validation and health

Status на `2026-04-15`: `Iteration 2` завершена. Помимо базового preflight и runbook, installer теперь умеет собирать structured runtime diagnostics из `docker compose ps --format json`, читать sanitized `worker` heartbeat snapshot и добавлять operator-facing troubleshooting hints в JSON-report без раскрытия секретов.

- preflight checks
- baseline runtime status report после `docker compose up`
- structured runtime service status в installer report
- worker heartbeat snapshot в installer report
- operator-friendly troubleshooting hints для partial failure и degraded runtime
- worker liveness/readiness signal через execution snapshot file без public endpoint
- операторская диагностика без раскрытия секретов

## Минимальный Definition of Done

`Installer MVP` можно считать готовым, когда:

- есть `Docker Compose`-based package для runtime contour
- install-time secrets не живут в репозитории и не прокидываются через runtime admin API
- installer вызывает существующие bootstrap-команды, а не дублирует их логику
- повторный запуск безопасен и документирован
- оператор получает проверяемый happy path: install -> migrate -> bootstrap admin -> start -> login в `Admin UI`

## Следующий практический шаг после закрытия Installer MVP

Текущий installer shell больше не требует completion внутри этого документа. Следующий delivery шаг смещен выше по уровню:

1. решить, нужен ли отдельный `Bootstrap Agent` или иной выделенный `setup plane`
2. формализовать host-level lifecycle beyond текущих `install/update/recover` сценариев
3. увязать это с общим `MVP` backlog по observability, support flows и pilot rollout

## Рекомендуемые итерации

### Iteration 1. Engine contract hardening

Status:

- завершена на `2026-04-15`

Цель:

- формализовать install manifest или equivalent input contract
- добавить structured validation errors
- добавить structured step results и sanitized status/report model
- усилить unit-style tests для `Installer.Common.ps1`

Выход итерации:

- installer остается script-first, но уже готов стать backend-движком для будущего UI

Checkpoint:

- после green tests и обновления vault лучше завершить сессию и очистить контекст перед следующим шагом

Фактический результат:

- добавлен `infra/scripts/Installer.Contract.ps1`
- `install.ps1` теперь строит manifest и умеет писать sanitized JSON-report через `-ReportJsonPath`
- validation теперь отдает structured issues вместо только строковых исключений
- execution plan теперь отдает machine-readable step results с stable step ids
- unit-style tests дополнены проверкой manifest/report и тем, что report не содержит secret material

### Iteration 2. Recovery and diagnostics hardening

Status:

- завершена на `2026-04-15`

Цель:

- усилить `update/recover` path
- расширить post-start diagnostics для `worker`
- сделать operator-friendly troubleshooting без утечки секретов
- довести runbook до сценариев partial failure

Выход итерации:

- recovery path перестает быть baseline-only и становится пригодным для реальной эксплуатации

Checkpoint:

- после фиксации runbook и тестов снова остановиться и очистить контекст

Фактический результат:

- `install.ps1` теперь поднимает `worker` через `docker compose up -d --wait worker`, а не fire-and-forget startup
- выделен `infra/scripts/Installer.Diagnostics.ps1` с парсингом structured runtime status и sanitized worker heartbeat snapshot
- JSON-report теперь включает `RuntimeStatus.Services`, `WorkerDiagnostics`, `DiagnosticIssues` и `TroubleshootingHints`
- installer помечает итог как `degraded`, если runtime жив, но diagnostics указывают на partial failure или blocked/degraded worker path
- `infra/tests/installer.common.tests.ps1` усилен проверкой structured diagnostics, degraded report outcome и worker wait-step
- `OTP/Delivery/Installer Operations Runbook.md` расширен под partial failure troubleshooting и новую структуру installer report

### Iteration 3. Separate local installer UI shell

Status:

- завершена на `2026-04-15`

Цель:

- спроектировать отдельный локальный wizard вне runtime `admin/`
- использовать только engine-контракт из первых двух итераций
- не дублировать orchestration rules в UI
- держать setup surface локальным и отключаемым после установки

Выход итерации:

- появляется operator-friendly setup UX без разрушения `ADR-025` и `ADR-029`

Checkpoint:

- после завершения UI shell и browser verification закрыть сессию и очистить контекст перед polish/follow-up

Фактический результат:

- добавлен отдельный root `installer-ui` на `React + Vite`, не встроенный в runtime `admin/`
- выбран тонкий loopback-only web shell, а не desktop-shell: orchestration остается в `infra/scripts/install.ps1`, а локальный UI только собирает input и рендерит sanitized report
- добавлен `installer-ui/server/installer-bridge.mjs`: локальный Node bridge на `127.0.0.1`, который в live-режиме запускает `install.ps1`, передает bootstrap password только через process env `OTPAUTH_ADMIN_PASSWORD` и не хранит его вне памяти процесса
- bridge поддерживает `mock` mode для repeatable browser verification без реального `docker compose` side-effect
- UI теперь показывает mode/form input, engine flags, validation issues, step results, runtime services, worker diagnostics и troubleshooting hints из machine-readable report
- `installer-ui` проходит `npm test`, `npm run build` и `npm run test:e2e`; browser verification подтверждена через checked-in `Playwright` сценарий на mock bridge

### Iteration 4. UI completion and operational closure

Status:

- завершена на `2026-04-15`

Цель:

- закрыть через UI happy path `install/update/recover`
- добавить оставшиеся guardrails, тесты и documentation updates
- сверить UI flow с runbook и final installer DoD

Выход итерации:

- installer становится end-to-end завершенным и готовым к первому controlled `on-prem` rollout

Checkpoint:

- после этой итерации делать полный context reset и начинать уже следующий delivery track отдельно

Фактический результат:

- `installer-ui` теперь типизирует не только `validation/issues`, но и `OperationProfile`, `Manifest` и `Configuration` из engine report
- UI добавляет mode-aware guardrails для live `Install` path: обязательный env file, bootstrap username/permissions и bootstrap password только там, где он реально нужен
- report теперь доводит оператора до operational closure: показывает sanitized handoff в runtime `Admin UI`, mode-specific next steps и итоговую сводку по `install/update/recover`
- `installer-ui` проходит `npm test` (`7` tests), `npm run build` и `npm run test:e2e`; из текущей среды browser regression потребовал запуск вне sandbox из-за локального `EPERM`
- первый `Installer MVP` по локальному `Docker Compose` path теперь закрывает end-to-end happy path `install -> bootstrap/start -> handoff -> login-ready Admin UI`; отдельный `Bootstrap Agent` остается следующим delivery track
