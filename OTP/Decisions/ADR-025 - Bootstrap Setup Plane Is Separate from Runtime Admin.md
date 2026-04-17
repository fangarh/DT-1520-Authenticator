# ADR-025: Bootstrap Setup Plane Is Separate from Runtime Admin

## Status

Accepted

## Context

К `2026-04-15` backend уже имеет рабочий bootstrap/runtime contour:

- `FluentMigrator`-based schema bootstrap
- explicit seed operations
- operational security maintenance commands
- готовый backend lifecycle для `TOTP` enrollment management

Это делает старт `Admin UI` практическим следующим шагом. Одновременно возник вопрос, можно ли запускать backend после ввода connection strings и секретов прямо из админки.

Такой подход конфликтует с уже зафиксированными ограничениями:

- secrets не должны храниться в репозитории или обычном app config
- bootstrap/seeding не должны выполняться автоматически на старте `Api`
- `on-prem` поставка должна оставаться пригодной для `Docker Compose` и future enterprise профилей
- `Admin UI` не должен становиться привилегированной оболочкой для host-level lifecycle операций

## Decision

- первичная установка и настройка выносятся в отдельный `bootstrap/setup plane`
- runtime `Admin UI` не запускает и не перезапускает backend-сервисы напрямую
- browser-based admin surface не получает host-level права на запись runtime secrets или выполнение команд запуска
- setup plane реализуется как отдельный installer, `Bootstrap Agent` или аналогичный локальный операционный контур
- setup plane отвечает за:
  - прием install-time параметров
  - валидацию окружения
  - безопасную запись конфигурации в platform secret store
  - запуск `ensure-database`, `migrate` и explicit seed/maintenance bootstrap операций
  - старт runtime units и health checks
- для ближайшего delivery-порядка принимается последовательность:
  - сначала зафиксировать architecture/design setup plane
  - затем реализовать `Admin UI MVP`
  - затем реализовать `Installer MVP` перед первым реальным `on-prem` rollout

## Consequences

- `admin/` остается runtime operator UI, а не installer shell
- bootstrap/install security boundary становится совместимой с `on-prem` и future enterprise secret-management требованиями
- существующие migration/bootstrap команды в `OtpAuth.Migrations` становятся основой installer-контура
- lifecycle management deployable units требует отдельного design и не должен неявно попадать в обычный runtime API
- реализация installer откладывается до момента, когда будут зафиксированы детали secret storage, host/runtime orchestration и rollout/update flow
