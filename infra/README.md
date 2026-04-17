# Infra

Этот каталог теперь содержит первый packaging slice для `Installer MVP`.

Текущие артефакты:

- `docker-compose.yml` - runtime contour `postgres + redis + api + worker + admin`
- `docker/` - Dockerfile для `api`, `worker`, `admin` и отдельного `bootstrap` image на базе `OtpAuth.Migrations`
- `nginx/admin.conf` - HTTPS edge для `Admin UI` и reverse proxy на `api`
- `env/runtime.env.example` - пример install-time/runtime env contract без секретов в репозитории
- `scripts/install.ps1` - installer entry point с режимами `Install`, `Update`, `Recover`
- `scripts/Installer.Contract.ps1` - machine-readable manifest/report contract для installer engine
- `scripts/Installer.Diagnostics.ps1` - structured runtime diagnostics и troubleshooting hints для installer report
- `scripts/Installer.Common.ps1` - testable helper-функции installer-контурa
- `../installer-ui/` - отдельный local setup shell на `React + Vite` с loopback-only bridge к `install.ps1`
- `tests/packaging.contract.tests.ps1` - contract checks для packaging/install assets
- `tests/installer.common.tests.ps1` - unit-style tests для env parsing, config validation и install plan

## Security boundary

- runtime secrets не хранятся в git-tracked файлах
- `admin` публикуется только по `HTTPS`, чтобы cookie-based admin contour не ломался на `Secure` cookies
- `api` остается внутренним сервисом compose-сети; `admin` в этом первом slice выступает HTTPS edge и проксирует `/api/*` и `/oauth2/*`
- install-time bootstrap вынесен в отдельный `bootstrap` profile и использует уже существующий `OtpAuth.Migrations`, а не runtime `Admin UI`

## Happy path

1. Скопировать `infra/env/runtime.env.example` в защищенное host-level место вне репозитория и подставить реальные значения.
2. Подготовить TLS certificate и private key на хосте, затем указать абсолютные пути в `OTPAUTH_TLS_CERT_PATH` и `OTPAUTH_TLS_KEY_PATH`.
3. Прогнать installer preflight:

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -PreflightOnly
```

4. Выполнить полный install path:

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env
```

5. Для обновления runtime без повторного bootstrap admin использовать update path:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Update
```

6. Для восстановления после restart/crash без rebuild и без bootstrap использовать recovery path:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Recover
```

7. При необходимости использовать installer как dry-run planner:

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -DryRun
```

8. Если нужен machine-readable отчет для будущего local setup UI или automation:

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
powershell -ExecutionPolicy Bypass -File .\infra\scripts\install.ps1 -EnvFilePath C:\secure\otpauth\runtime.env -Mode Install -ReportJsonPath C:\secure\otpauth\installer-report.json
```

9. Если нужен ручной bootstrap path без installer orchestration, он остается доступен:

```powershell
docker compose --env-file C:\secure\otpauth\runtime.env -f .\infra\docker-compose.yml up -d postgres redis
docker compose --env-file C:\secure\otpauth\runtime.env -f .\infra\docker-compose.yml --profile bootstrap run --rm bootstrap ensure-database
docker compose --env-file C:\secure\otpauth\runtime.env -f .\infra\docker-compose.yml --profile bootstrap run --rm bootstrap migrate
```

10. Для ручного path bootstrap admin user можно создать отдельной одноразовой командой:

```powershell
$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'
docker compose --env-file C:\secure\otpauth\runtime.env -f .\infra\docker-compose.yml --profile bootstrap run --rm bootstrap upsert-admin-user operator enrollments.read enrollments.write
```

11. Поднять runtime:

```powershell
docker compose --env-file C:\secure\otpauth\runtime.env -f .\infra\docker-compose.yml up -d api worker admin
```

12. Открыть `https://<host>:8443/`.

## Installer behavior

- installer fail-closed отвергает env-файл внутри репозитория
- режим `Install` выполняет полный `build -> bootstrap -> bootstrap admin -> runtime startup`
- режим `Update` выполняет `build -> migrate -> runtime restart` без повторного bootstrap admin
- режим `Recover` поднимает существующий runtime contour без rebuild и без bootstrap-операций
- `worker` во всех режимах поднимается через `docker compose up -d --wait worker`, чтобы unhealthy background runtime ломал recovery path fail-closed, а не прятался за `running`
- preflight проверяет `docker`, `docker compose`, writable env directory и наличие TLS файлов; свободный `OTPAUTH_ADMIN_HTTPS_PORT` обязателен только для `Install`, а `Update/Recover` допускают уже занятый порт текущей инсталляцией
- bootstrap admin password берется только из process-level `OTPAUTH_ADMIN_PASSWORD`
- `-DryRun` печатает план шагов без запуска `docker compose`
- installer engine теперь имеет machine-readable contract: manifest, structured validation issues, step results и sanitized JSON-report через `-ReportJsonPath`
- `worker` теперь публикует heartbeat в `/tmp/otpauth-worker/heartbeat.json`, а `docker compose ps` показывает его health status вместе с остальными runtime services
- после runtime startup installer печатает `docker compose ps` как sanitized status report без вывода секретов
- JSON-report теперь дополнительно хранит structured `RuntimeStatus.Services`, `WorkerDiagnostics`, `DiagnosticIssues` и `TroubleshootingHints`
- JSON-report умышленно не содержит `ConnectionStrings__Postgres`, signing keys, `TotpProtection` keys или bootstrap admin password
- подробный operational сценарий вынесен в `OTP/Delivery/Installer Operations Runbook.md`

## Validation

Локальная contract-проверка:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\tests\packaging.contract.tests.ps1
```

Unit-style tests для installer helpers и engine contract:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\tests\installer.common.tests.ps1
```

Локальная проверка отдельного installer UI shell:

```powershell
cd .\installer-ui
npm test
npm run build
npm run test:e2e
```

Следующие слайсы:

- отдельный `Bootstrap Agent`, если setup plane потребуется отделить и от текущего script-first installer UI
- отдельный ingress/API exposure contract, если понадобится разделить operator UI и public integration endpoint
