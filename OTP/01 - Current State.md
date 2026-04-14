# Current State

## Статус проекта

Проект находится на стадии архитектурного проектирования.

Реализовано сейчас:

- базовый Obsidian vault в `OTP/`
- архитектурные заметки по платформе `2FA/MFA`
- агентский протокол работы через vault
- верхнеуровневая структура папок для дальнейшей систематизации заметок
- доменные индексы по `Architecture`, `Data`, `Integrations`, `Product`, `Delivery`
- черновой `ERD`
- черновой `OpenAPI v1`
- расширенный `OpenAPI v1` draft с `security`, `webhooks`, `Problem`-ошибками и idempotency
- auth/token модель для integration clients и mobile devices зафиксирована в vault и контракте
- `openapi-v1.yaml` прогнан через `Redocly CLI` и проходит валидацию
- утвержден `MVP` technology baseline
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

Пока не реализовано:

- backend
- mobile app
- `API`
- схема БД в виде миграций
- инфраструктурные манифесты
- тесты

## Текущая продуктовая рамка

Целевой продукт: отдельный `2FA Server`, подключаемый к существующим системам.

Базовые факторы первой очереди:

- `TOTP`
- `Push approval`
- `Backup codes`

## Текущая архитектурная позиция

- стартовать с `REST-first` интеграции
- первый релиз строить как `modular monolith`
- использовать `ASP.NET Core + PostgreSQL + Redis + Outbox/Worker`
- mobile factor первой версии делать как `Android` app на `Kotlin`
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
