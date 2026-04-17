# Bootstrap Installer Plane

## Status

Accepted working guideline

## Цель

Зафиксировать безопасный контур первичной установки и настройки `on-prem` поставки так, чтобы `Admin UI` не превращался в привилегированную панель запуска runtime-сервисов.

## Базовый принцип

`Runtime Admin UI` и `bootstrap/setup plane` разделяются.

- `Admin UI` работает только после того, как система уже развернута и доступна
- браузерная админка не получает права на запуск/остановку backend-процессов или запись секретов в host-level конфигурацию
- первичная настройка, проверка окружения, запуск миграций и старт сервисов выполняются отдельным `Bootstrap Agent` или installer-контуром

## Почему это нужно

Если сделать запуск backend-а обычной функцией `Admin UI`, появляются опасные свойства:

- UI начинает требовать host-level привилегии
- секреты и connection strings проходят через runtime HTTP API
- появляется цикл зависимости, где UI зависит от backend-а, который она же должна поднять
- возрастает риск утечки секретов в логи, audit payload или transport telemetry

Для `on-prem` и future enterprise профилей это слишком слабая security boundary.

## Целевой контур

### Runtime plane

- `OtpAuth.Api`
- `OtpAuth.Worker`
- `PostgreSQL`
- `Redis`
- `Admin UI`

### Bootstrap/setup plane

- отдельный installer или `Bootstrap Agent`
- локальная setup UI или wizard
- команды первичной проверки окружения
- запись runtime-конфигурации в platform secret store
- запуск миграций и optional bootstrap seed
- старт и health-check deployable units

## Что делает setup plane

1. Принимает параметры установки:
   - `ConnectionStrings__Postgres`
   - runtime secrets и key references
   - deployment profile
   - базовые host/runtime настройки
2. Проверяет конфигурацию:
   - доступность `PostgreSQL`
   - корректность обязательных секретов и key metadata
   - готовность runtime окружения
3. Сохраняет конфигурацию в безопасное runtime-хранилище:
   - `Vault/KMS`, если доступен
   - иначе platform secrets (`Docker secrets`, host-level protected env/config store)
4. Выполняет bootstrap-команды:
   - `ensure-database`
   - `migrate`
   - optional explicit seed operations
5. Поднимает runtime units:
   - `Api`
   - `Worker`
   - инфраструктурные зависимости в рамках выбранной формы поставки
6. Возвращает оператору итог установки и только после этого передает работу обычному `Admin UI`

## Что setup plane не должен делать

- не должен быть частью обычного публичного runtime API
- не должен хранить секреты в репозитории, markdown, `appsettings` или обычных БД-таблицах приложения
- не должен повторно показывать секреты после сохранения
- не должен превращать `Admin UI` в оболочку для удаленного выполнения host-команд

## Security requirements

- bootstrap contour доступен только локально или через явно выделенный защищенный операционный канал
- setup transport не публикуется как обычный internet-facing endpoint
- все чувствительные значения маскируются в UI, логах и диагностике
- install/update операции должны быть audit-friendly без сохранения secret material
- bootstrap должен быть idempotent и устойчив к частично успешному выполнению
- после завершения первичной настройки bootstrap surface должен быть отключаемым или жестко ограниченным

## Рекомендуемая форма для `MVP`

Для первой `on-prem` поставки ориентиром считается:

- `Docker Compose` как базовая форма развертывания
- отдельный installer/setup contour поверх него
- runtime `Admin UI` без прав на lifecycle management deployable units

## Рекомендуемый порядок delivery

1. Зафиксировать architecture/design для `bootstrap/setup plane`.
2. Реализовать `Admin UI MVP` поверх уже готового enrollment management backend.
3. Реализовать `Installer MVP` перед первым реальным `on-prem` rollout.

## Следствия для текущего репозитория

- текущий `admin/` остается runtime-панелью операторских действий, а не installer-оболочкой
- текущие bootstrap и maintenance команды в `backend/OtpAuth.Migrations` остаются основой для будущего installer-контура
- отдельный management surface для lifecycle deployable units нужно проектировать вне обычного `OtpAuth.Api`
- если для installer добавляется UI, он должен быть отдельным локальным setup surface, а не расширением runtime `admin/`
- installer UI не должен дублировать orchestration-логику; source of truth для setup behavior остается в installer engine
