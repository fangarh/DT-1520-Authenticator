# Android TOTP-First Plan

## Status

Accepted working guideline

## Цель

Зафиксировать первый рабочий трек для `mobile/` так, чтобы после промежуточной очистки контекста можно было продолжить реализацию без повторного анализа всего репозитория.

## Основания

- [[Mobile App]]
- [[Android App Bootstrap]]
- [[../Delivery/Admin and Android Readiness Gates]]

## Текущее состояние на `2026-04-15`

- backend enrollment contract уже стабилизирован для `TOTP-first` mobile slice
- `Admin UI MVP` и `Installer MVP` закрыты на уровне текущего `MVP`
- `mobile/` больше не должен развиваться как один `app`-модуль поверх `Empty Activity` scaffold
- в bootstrap-заметке был старый путь проекта; после переноса workspace канонический путь теперь `D:\Projects\2026\DT-1520-Authenticator\mobile`

## Scope текущего трека

Входит:

- clean multi-module layout для `Android`
- secure storage abstraction поверх `Android Keystore`
- parsing/import provisioning artifact для `TOTP`
- локальная генерация `TOTP`
- offline runtime screen с текущими кодами

Не входит:

- `push approval`
- device registry / activation / revoke
- `FCM`
- mobile session/token lifecycle
- self-service enrollment
- installer/setup-plane логика внутри mobile app

## Итерации

### Step 1. Vault fixation and reset checkpoint

Status:

- completed on `2026-04-15`

Выход:

- этот план стал канонической точкой продолжения mobile-трека
- `Current State`, `Implementation Map` и session log должны ссылаться на этот трек
- после очистки контекста читать в порядке: `Current State -> Implementation Map -> this note -> Android App Bootstrap`

### Step 2. Mobile module layout

Status:

- completed on `2026-04-15`

Целевая раскладка:

- `:app`
- `:core:ui`
- `:feature:provisioning`
- `:feature:totp-codes`
- `:security:storage`
- `:totp-domain`

Правила:

- `app` только собирает shell и wiring
- `totp-domain` не зависит от Android
- `security-storage` изолирует хранение и future `Keystore` integration
- feature-модули не владеют storage и не знают про installer/backend lifecycle

### Step 3. Secure storage abstraction

Status:

- completed on `2026-04-15`

Выход:

- `security:storage` теперь имеет явный контракт `SecureTotpSecretStore` для `list/read/save/delete`
- добавлена Android-реализация поверх `SharedPreferences + Android Keystore AES/GCM` без прямой зависимости feature-модулей от `Keystore`
- persistence хранит только hashed storage key и шифрованный payload; account metadata и secret material не уходят в plain-text key names
- добавлены unit tests на record validation, serialization roundtrip, restore/delete и corrupted record fail-closed path
- security review для шага пройден: hardcoded secrets/logging отсутствуют, corrupted payload не silently skip-ается, а приводит к fail-closed `SecureTotpSecretStorageException`

### Step 4. TOTP domain engine

Status:

- completed on `2026-04-17`

Выход:

- в `totp-domain` добавлен pure-Kotlin parser для `otpauth://totp/...` без Android-зависимостей
- validation теперь fail-closed покрывает `issuer/label/secret/digits/period/algorithm`, duplicate query params и invalid percent-encoding
- добавлены redacted secret-bearing value objects, чтобы secret material не уходил в `toString()` и operator-facing ошибки
- реализована RFC-backed `TOTP` generation для `SHA1/SHA256/SHA512`
- добавлен countdown/state model для будущего offline code screen
- unit tests закрывают RFC-векторы, parser, UTF-8 decoding, Base32 normalization и security-sensitive redaction paths

### Step 5. Provisioning flow

Status:

- completed on `2026-04-17`

Выход:

- `feature:provisioning` теперь принимает masked `otpauth://` URI и manual fallback input
- импорт больше не идет напрямую в storage: сначала строится preview без повторного показа secret material, затем отдельный confirm/save
- save boundary остается в `app`, а feature знает только про secure-save callback, не про `Keystore` implementation
- `security:storage` snapshot расширен до `digits + algorithm`, чтобы provisioning flow не терял `TOTP` metadata
- unit tests закрывают invalid/valid import path, preview reducer и save feedback path

### Step 6. Runtime codes screen

Status:

- completed on `2026-04-17`

Выход:

- `feature:totp-codes` больше не является placeholder: route теперь показывает список сохраненных аккаунтов, текущий код и countdown
- runtime screen строится только из secure store snapshot; `app` больше не держит последний imported preview с secret-bearing credential в UI state после save
- добавлен local remove flow с явным confirm-state, без мгновенного destructive удаления по первому tap
- корневой `app` shell переведен на scrollable layout, чтобы нижняя часть runtime-card и remove flow были достижимы на реальном устройстве
- unit tests добавлены для runtime presenter, remove workflow и app-side secure store catalog wiring
- live verification пройдена на `emulator-5554`: manual import -> preview -> save -> runtime code/countdown -> remove confirm state подтверждены через `Android Studio MCP`, `the_android_mcp` и `mobile_mcp`

### Step 7. Backend and vault contract sync

Status:

- completed on `2026-04-17`

Выход:

- stable provisioning contract зафиксирован в [[../Integrations/TOTP Provisioning Contract]]
- backend/vault references для `secretUri` и правил visibility выровнены между `Integrations`, `Product`, `Delivery` и `Security`
- `OpenAPI` дополнен явным описанием того, что `secretUri` и `qrCodePayload` являются artifact fields и не должны ожидаться вне `start/replace`
- `Current State`, `Implementation Map` и session log обновлены под фактическое состояние mobile slice

## План добивания mobile после Step 7

Цель этого плана: закрыть `Definition of Done` для `Android TOTP-first`, не смешивая текущий локальный `TOTP` slice с `push`, device lifecycle и mobile token/session contour.

### 1. Compose UI test contour

- добавить instrumented/UI tests для provisioning route: invalid input, preview, confirm/save feedback
- добавить instrumented/UI tests для runtime codes route: list render, remove confirm state и safe empty-state
- не дублировать domain/storage unit tests в UI-слое; UI tests должны проверять wiring и отсутствие регрессии на реальном Compose surface

### 2. Mobile hardening verification

- прогнать полный mobile contour: `:app:testDebugUnitTest`, feature/storage/domain tests и `:core:ui:assembleDebug`
- прогнать новый `androidTest` contour для provisioning/runtime screens
- повторить live verification на `emulator-5554` или эквивалентном эмуляторе: import -> preview -> save -> runtime code -> remove confirm

### 3. Security closure

- проверить, что secret-bearing inputs и preview state не переживают save/reload path дольше необходимого
- проверить, что UI/instrumented tests и live flow не выявляют утечки secret material в logs, crash output и persistent caches
- если найдены новые ограничения для mobile shell, обновить [[../Security/Security Model MVP]] и related notes сразу в рамках этого трека

### 4. TOTP-first closure

- после прохождения UI/instrumented tests и live verification обновить `Current State`, `Implementation Map` и session log
- пометить `Android TOTP-first` как закрытый локальный slice
- только после этого переходить к следующему уровню mobile/backlog work: `backup codes` backend slice, device lifecycle design/contracts, затем `push`

## Definition of Done для `Android TOTP-first`

- приложение принимает provisioning artifact и сохраняет секрет безопасно
- приложение генерирует корректный `TOTP` офлайн
- есть unit tests для domain/storage и базовые UI tests для provisioning/runtime screens
- security review не находит утечек secret material в логах, error payloads и постоянном клиентском кэше

## Checkpoint for Context Reset

Если сессия очищена после `Step 7`, не нужно заново исследовать installer/admin track.

Нужно продолжать так:

1. проверить `mobile/settings.gradle.kts` и фактические модули `:app`, `:core:ui`, `:feature:provisioning`, `:feature:totp-codes`, `:security:storage`, `:totp-domain`
2. проверить `mobile/security/storage/*` и убедиться, что `SecureTotpSecretStore` уже реализован поверх `Android Keystore`
3. проверить `mobile/totp-domain/*` и убедиться, что parser/code generator/countdown model уже реализованы и покрыты unit tests
4. проверить `mobile/feature/provisioning/*` и убедиться, что masked import/preview/save flow уже реализован и storage сохраняет `digits + algorithm`
5. прочитать [[../Integrations/TOTP Provisioning Contract]] и не предполагать повторную выдачу artifact вне `start/replace`
6. продолжать с `План добивания mobile после Step 7`, а не возвращаться к уже закрытым `Step 1-7`
7. не смешивать `push`, device lifecycle и `Bootstrap Agent` в этот mobile slice
