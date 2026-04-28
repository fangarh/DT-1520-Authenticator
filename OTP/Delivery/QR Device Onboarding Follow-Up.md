# QR Device Onboarding Follow-Up

## Status

Completed on `2026-04-27`.

`Iteration 1` closed on `2026-04-27` as backend/admin contract foundation.

`Iteration 2` closed on `2026-04-27` as Admin UI QR workspace.

`Iteration 3` closed on `2026-04-27` as Android QR scan/import path.

`Iteration 4` closed on `2026-04-27` as end-to-end hardening and documentation closure.

## Goal

Подготовить и затем реализовать полноценный QR-based onboarding flow для мобильного приложения, чтобы оператор мог безопасно передавать пользователю одноразовый activation artifact без ручного копирования кодов.

## Why this matters

Текущий `Device Registry` уже имеет server-side activation artifact model через `auth.device_activation_codes`, но operator-ready UX для выдачи такого артефакта пользователю пока не оформлен.

Без этого:

- onboarding устройства остается техническим, а не productized
- первый `push` pilot зависит от ручной подготовки activation path
- mobile rollout сложнее объяснять и воспроизводить support/operator-ам

## Target outcome

После закрытия этой задачи система должна уметь:

- сгенерировать одноразовый device onboarding artifact из admin/operator surface
- представить его пользователю в QR-friendly форме
- позволить mobile app считать QR и завершить activation flow
- автоматически погасить artifact после успешного consume
- не оставлять reusable onboarding links после scan/activation

## Current design premise

На текущий момент наиболее вероятная каноническая модель такая:

- `Admin UI` или операторский backend flow создает one-time activation artifact
- artifact имеет TTL и server-side status
- QR кодирует не долгоживущую ссылку, а короткоживущий opaque activation payload
- mobile app сканирует QR и вызывает existing activation path
- server consume-ит artifact атомарно в activation flow
- отдельная post-scan команда "удалить ссылку" не должна быть единственным механизмом инвалидирования

## Discussed result

Результат обсуждения зафиксирован так:

- оператор в `Admin UI` инициирует `Выдать QR для подключения устройства`
- backend создает одноразовый onboarding artifact поверх existing `auth.device_activation_codes`
- artifact должен быть привязан как минимум к `tenantId`, `applicationClientId`, `externalUserId`, иметь `TTL` и при необходимости `platform`
- `Admin UI` показывает QR пользователю
- mobile app сканирует QR и использует existing `POST /api/v1/devices/activate`
- server атомарно consume-ит artifact внутри activation flow
- после успешной активации artifact автоматически становится недействительным

Для первого implementation slice предпочтение отдано `opaque activation payload`, а не deep-link-first подходу.

Первичный security contract фиксируется server-side:

- `one-time`
- `expiresAt`
- `consumedAt`
- optional `revokedAt`

Команда со стороны телефона на "отключение/удаление ссылки" допустима только как вторичный UX signal. Она не должна быть единственным механизмом защиты и не должна подменять server-side invalidation semantics.

## Important design caution

Идея "после сканирования телефон передает команду об отключении/удалении ссылки" сама по себе недостаточна как основной security mechanism.

Почему:

- scan еще не означает успешный activation
- app может упасть после scan и до confirm
- link может быть просканирован, но не завершен
- отдельная `delete link` команда создает race и лишнюю точку отказа

Поэтому базовый security contract должен быть server-side:

- artifact либо consume-ится атомарно в `activate`
- либо истекает по TTL
- либо явно revoke-ится оператором

А post-scan invalidation со стороны телефона можно рассматривать только как дополнительный UX signal, но не как единственный security barrier.

## Open design questions

Перед реализацией нужно согласовать:

1. Что именно кодируется в QR:
   - first-choice: opaque activation payload
   - альтернативно: короткая HTTPS link
   - отдельно, только если понадобится: custom app link / deep link
2. Где генерируется QR:
   - в `Admin UI`
   - на backend с уже готовым QR payload
3. Какой UX нужен оператору:
   - показать QR на экране
   - выдать короткую ссылку
   - распечатать / передать изображение
4. Какой TTL нужен:
   - минуты
   - часы
5. Нужен ли explicit revoke path для уже созданного, но не использованного artifact
6. Нужно ли связывать artifact с:
   - `tenantId`
   - `applicationClientId`
   - `externalUserId`
   - platform
7. Нужен ли single-device intent per artifact или допустим повторный выпуск нового QR для того же пользователя без revoke старого

## Minimum implementation expectations

Когда задача пойдет в реализацию, минимум должен включать:

- admin/backend flow для create/list/revoke pending onboarding artifacts
- QR-friendly payload contract
- mobile scanning/import path
- atomарный consume в device activation flow
- automated tests
- security review

## Iteration Plan

### Iteration 0. Contract preflight

Status: completed implicitly before implementation start.

Решения:

- первый production-oriented flow идет через opaque activation payload, а не deep-link-first contract
- artifact остается server-side one-time entity поверх `auth.device_activation_codes`
- primary invalidation: atomic consume в `POST /api/v1/devices/activate`, TTL или explicit operator revoke
- phone-side "delete scanned link" не является security boundary
- первый supported platform для QR onboarding: `android`

### Iteration 1. Backend/admin artifact contract

Status: completed on `2026-04-27`.

Реализовано:

- admin API для create/list/revoke device onboarding artifacts:
  - `GET /api/v1/admin/tenants/{tenantId}/device-onboarding-artifacts`
  - `POST /api/v1/admin/device-onboarding-artifacts`
  - `POST /api/v1/admin/tenants/{tenantId}/device-onboarding-artifacts/{activationCodeId}/revoke`
- create требует `devices.write` + `CSRF`, генерирует activation payload server-side и возвращает plaintext только в command response
- list требует `devices.read` и не возвращает activation payload или hash
- revoke требует `devices.write` + `CSRF`
- `auth.device_activation_codes` расширена `revoked_utc`
- `ActivateDeviceHandler` fail-closed отклоняет revoked activation artifact
- audit пишет sanitized `admin_device_onboarding.created|revoked` без activation payload/hash
- targeted backend verification: `AdminDeviceOnboarding|DeviceApiTests` зеленый (`26/26`)
- full backend verification: `backend/scripts/verify-backend.ps1` зеленый (`354/354` infra tests, `19/19` worker tests); residual warning прежний и связан только с `IBM.Data.Db2` architecture mismatch в migrations
- docs verification после handoff updates: `docs npm test`, `npm run build`, `npm run test:e2e` зеленые

Security review:

- operator-provided activation payload отклоняется
- plaintext activation payload не хранится в БД и не попадает в list/read response
- `code_hash` не возвращается наружу
- revoked/consumed/expired artifacts нельзя использовать для activation
- state-changing admin endpoints требуют `CSRF`

### Iteration 2. Admin UI QR workspace

Status: completed on `2026-04-27`.

Goal:

- добавить operator workspace для выдачи QR device onboarding artifact
- показать generated payload/QR только в текущем UI state
- дать list/revoke pending artifacts без раскрытия payload

Реализовано:

- `admin/src/features/device-onboarding` добавляет отдельный operator workspace для `devices.read|write`
- shared admin API client поддерживает `list/create/revoke` для `/api/v1/admin/.../device-onboarding-artifacts`
- QR рендерится через `qrcode.react` как `QRCodeSVG` с opaque activation payload
- generated activation payload показывается только в текущем React state и очищается через discard, reload/list, selection change, revoke и follow-up commands
- list/detail path показывает только sanitized artifact metadata без activation payload/hash
- revoke доступен только для `pending` artifact и требует explicit operator confirmation
- checked-in Playwright regression покрывает load, create, QR display, discard, revoke, browser storage non-persistence и отсутствие horizontal overflow

Exit criteria:

- component tests покрывают create/list/revoke, one-time display и discard behavior
- Playwright проверяет operator flow и отсутствие overflow/overlap
- security review подтверждает отсутствие persistence of activation payload в browser storage

Verification:

- `admin npm test` зеленый (`52/52`)
- `admin npm run build` зеленый
- `admin npm run test:e2e` зеленый (`7/7`)
- `admin npm audit` и `npm audit --omit=dev` без уязвимостей

Security review:

- activation payload не пишется в `localStorage`/`sessionStorage`
- activation payload не появляется в list/detail/fixture read response
- state-changing create/revoke идут через shared `CSRF`-protected admin API client
- QR не использует external embedded image/logo settings, чтобы не добавлять CORS/tainted-canvas или remote asset surface

### Iteration 3. Android QR scan/import path

Status: completed on `2026-04-27`.

Goal:

- заменить debug-only activation helper production-oriented QR scan/import flow
- QR payload передается в device runtime activation flow без integration secret/token в mobile app
- activation failures показываются sanitized copy

Реализовано:

- backend дополнен tokenless mobile consume endpoint-ом `POST /api/v1/devices/activate-onboarding`, который принимает one-time `activationPayload` и device metadata
- endpoint не требует integration bearer на устройстве: tenant/application/external user берутся из server-side activation artifact, а artifact валидируется/consume-ится атомарно тем же `ActivateDeviceHandler`
- `mobile` получил новый модуль `:feature:device-onboarding` с validation/workflow contracts, manual fallback input, scan action и sanitized failure copy
- `mobile/app` подключает real QR scan activity через `CameraX` preview + `ML Kit Barcode Scanning` с `FORMAT_QR_CODE`
- QR payload передается в `DeviceRuntimeSessionManager.activateWithOnboardingPayload`, который сохраняет issued device session в encrypted device session store
- debug-only `PilotDeviceActivationActivity` остается только pilot tooling, а production-oriented path теперь доступен из основного app UI

Exit criteria:

- unit tests покрывают QR payload validation/import workflow
- Android UI/instrumented tests покрывают happy path и fail-closed invalid payload
- live MCP verification на emulator подтверждает scan/import/startup path

Verification:

- backend targeted `DeviceApiTests` зеленые (`19/19`)
- full backend verification зеленый: `backend/scripts/verify-backend.ps1` (`357/357` infra tests, `19/19` worker tests); residual warning прежний и связан только с `IBM.Data.Db2` architecture mismatch в migrations
- Android unit verification зеленая: `:feature:device-onboarding:testDebugUnitTest :app:testDebugUnitTest`
- Android build зеленый: `:app:assembleDebug`
- Android instrumented verification зеленая на `Pixel_10_Pro`: `DeviceOnboardingUiTest` (`2/2`)
- live MCP smoke на `emulator-5554`: app launch, `Device onboarding` section, `Scan QR` action and camera scanner screen confirmed

Security review:

- mobile QR payload is treated as one-time credential-like material and is not persisted after successful activation
- mobile app does not store or receive integration `client_secret` or integration bearer token for QR activation
- `activate-onboarding` derives tenant/application/external user from server-side artifact instead of trusting QR-provided claims
- invalid/expired/revoked/consumed/wrong-platform artifacts return generic activation failure semantics
- scanner logs only generic camera/analysis failures and does not log QR payload

### Iteration 4. End-to-end hardening and docs closure

Status: completed on `2026-04-27`.

Goal:

- пройти backend + admin + Android verification как единый onboarding flow
- обновить `docs/`, `admin/README.md`, `backend/README.md`, vault и session note
- убрать/понизить debug-only activation helper из pilot handoff

Реализовано:

- full backend verification зеленый: `backend/scripts/verify-backend.ps1` (`357/357` infra tests, `19/19` worker tests)
- `admin` verification зеленая: `npm test` (`52/52`), `npm run build`, `npm run test:e2e` (`7/7`)
- `docs` verification зеленая: `npm test`, `npm run build`, `npm run test:e2e`
- Android verification зеленая: `:feature:device-onboarding:testDebugUnitTest :app:testDebugUnitTest :app:assembleDebug` и `:app:connectedDebugAndroidTest` (`14/14`) на `Pixel_10_Pro`
- live MCP smoke подтвердил installed app launch на `emulator-5554`, `MainActivity`, видимый `Device onboarding`, `Scan QR` и manual payload fallback без package crash в diagnostics
- debug-only `PilotDeviceActivationActivity` оставлен только как `src/debug` pilot tooling и исключен из production onboarding handoff

Security review:

- activation payload остается credential-like one-time material: plaintext виден только в Admin UI create response/current UI state и не появляется в list/detail/read responses
- Android QR activation не получает integration `client_secret` или bearer token, а binding берется из server-side artifact
- consume/revoke/TTL остаются server-side security boundary; phone-side scan сам по себе не считается invalidation
- browser storage, audit payloads, backend list response, mobile secure store и debug helper handoff не получают raw activation payload сверх явно разрешенного one-time display/import path

## Continuation point

Следующий practical continuation point: [[Official Dotnet Integration SDK]].
