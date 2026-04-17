# Installer Operations Runbook

## Status

Accepted working guideline

## Цель

Зафиксировать безопасный operational порядок для `install`, `update` и `recovery` поверх текущего `Docker Compose`-based `Installer MVP`, не смешивая runtime `Admin UI` с host-level lifecycle операциями.

## Опорные артефакты

- `infra/scripts/install.ps1`
- `infra/scripts/Installer.Common.ps1`
- `infra/docker-compose.yml`
- `infra/env/runtime.env.example`

## Security boundary

- runtime env file живет вне репозитория
- `OTPAUTH_ADMIN_PASSWORD` подается только через process env
- installer не печатает полный render `docker compose config`
- installer не печатает `ConnectionStrings__Postgres`, signing keys, `TotpProtection` keys или admin password
- runtime `Admin UI` не получает installer privileges

## Режимы installer

### Install

Использовать для первого разворачивания новой инсталляции.

Шаги по умолчанию:

1. `docker compose build api worker admin bootstrap`
2. `docker compose up -d --wait postgres redis`
3. `bootstrap ensure-database`
4. `bootstrap migrate`
5. `bootstrap upsert-admin-user`
6. `docker compose up -d --wait api admin`
7. `docker compose up -d --wait worker`
8. `docker compose ps`

Требования:

- env file вне repo
- доступный `OTPAUTH_ADMIN_HTTPS_PORT`
- process-level `OTPAUTH_ADMIN_PASSWORD`

### Update

Использовать для controlled rollout новой версии поверх существующей инсталляции.

Шаги по умолчанию:

1. `docker compose build api worker admin bootstrap`
2. `docker compose up -d --wait postgres redis`
3. `bootstrap ensure-database`
4. `bootstrap migrate`
5. `docker compose up -d --wait api admin`
6. `docker compose up -d --wait worker`
7. `docker compose ps`

Семантика:

- bootstrap admin по умолчанию не трогается
- занятый `OTPAUTH_ADMIN_HTTPS_PORT` считается нормальным, если это текущая инсталляция
- миграции остаются частью update path, потому что их idempotency уже является контрактом `OtpAuth.Migrations`

### Recover

Использовать после host restart, container crash или частичного падения runtime, когда rebuild и bootstrap не нужны.

Шаги по умолчанию:

1. `docker compose up -d --wait postgres redis`
2. `docker compose up -d --wait api admin`
3. `docker compose up -d --wait worker`
4. `docker compose ps`

Семантика:

- bootstrap не выполняется
- bootstrap admin не трогается
- режим не требует свободного admin HTTPS порта заранее

## Рекомендуемые команды

### Preflight для новой установки

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -PreflightOnly
```

### Полная установка

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Install
```

### Обновление

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Update
```

### Восстановление

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Recover
```

### Dry-run плана

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Update -DryRun
```

## Когда использовать ручной путь

Ручные `docker compose` или `bootstrap` команды допустимы, если нужен один точечный шаг:

- повторить только `migrate`
- вручную пересоздать bootstrap admin
- посмотреть runtime state отдельно от orchestration

Installer не должен блокировать такие операционные сценарии.

## Post-start проверка

Минимальный check после `Update` или `Recover`:

1. проверить вывод `docker compose ps`
2. если installer запускался с `-ReportJsonPath`, открыть JSON-report и проверить `Outcome`, `RuntimeStatus.Services`, `WorkerDiagnostics` и `TroubleshootingHints`
3. открыть `https://<host>:<OTPAUTH_ADMIN_HTTPS_PORT>/`
4. убедиться, что `admin` отвечает, а `/health/api` не падает
5. убедиться, что `worker` отображается как `healthy`, а не только `running`
6. при проблемах с background processing отдельно проверить контейнер `worker`

## Partial failure semantics

Если installer завершился как `degraded`, а не `failed`:

1. считать это признаком частичной operational проблемы, а не cosmetic warning
2. использовать `TroubleshootingHints` из JSON-report как первый источник triage
3. сверить `RuntimeStatus.Services` и `WorkerDiagnostics`, чтобы отделить container startup issue от dependency/job incident
4. только после этого решать, нужен ли повторный `Recover`, ручной `docker compose` шаг или dependency-level incident response

Если installer завершился `failed` на worker/startup участке:

1. считать это явным сбоем readiness path, а не просто задержкой фонового запуска
2. сначала смотреть structured runtime diagnostics и heartbeat-related hints
3. затем уже переходить к `docker compose logs --tail 100 worker`

## Worker-specific troubleshooting

Если `worker` не переходит в `healthy` или installer помечен как `degraded` из-за worker diagnostics:

1. проверить `docker compose ps worker`
2. посмотреть `docker compose logs --tail 100 worker`
3. открыть installer JSON-report и сверить `WorkerDiagnostics.ExecutionOutcome`, `ConsecutiveFailureCount`, `DependencyStatuses` и `JobStatuses`
4. убедиться, что внутри контейнера обновляется `/tmp/otpauth-worker/heartbeat.json`
5. если snapshot есть, но один из dependency probe постоянно `degraded`, считать это runtime dependency incident, а не просто проблемой compose
6. если `jobStatuses` показывают `blocked`, считать это признаком того, что job scheduling жив, но текущие зависимости не дают двигать работу дальше
7. если `jobStatuses` показывают `degraded` при healthy dependency probes, считать это incident на уровне конкретного background job, а не контейнера целиком
8. если heartbeat не обновляется, считать это признаком сбоя startup loop или blocked background execution, а не просто проблемой compose

Текущее состояние:

- installer report теперь хранит не только raw `docker compose ps`, но и structured service status через `docker compose ps --format json`
- `worker` startup в install/update/recover path теперь идет через `--wait`, чтобы recovery path fail-closed ловил unhealthy background runtime
- snapshot по-прежнему подтверждает не только dependency probes, но и domain-level progress по первому job `security_data_cleanup`: видны последний run, outcome и sanitized cleanup metrics
- будущие outbox/background jobs должны подключаться к тому же `jobStatuses` contract, а не изобретать отдельный diagnostics format

## Ожидаемый follow-up

- подключать будущие outbox/background jobs к уже существующему `jobStatuses` contract
- вынести machine-readable troubleshooting hints в будущий local installer UI без дублирования orchestration rules
- позже решить, нужен ли отдельный public ingress contour для `api`
