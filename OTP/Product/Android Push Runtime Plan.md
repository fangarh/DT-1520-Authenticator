# Android Push Runtime Plan

## Status

Accepted working guideline

## Цель

Зафиксировать следующий рабочий мобильный трек после закрытия `Android TOTP-first`: довести приложение до device-bound `push` runtime, не смешивая это с installer/setup-plane, `iOS` и future attestation work.

## Основания

- [[Mobile App]]
- [[../Architecture/Device Lifecycle Design]]
- [[../Integrations/Auth and Token Flows]]
- [[../Decisions/ADR-004 - Mobile App as TOTP and Push Factor]]
- [[../Decisions/ADR-030 - Device Registry Uses Rotating Refresh Tokens and Auth-State Invalidation]]
- [[../Decisions/ADR-031 - Push Challenges Are Bound to a Single Active Device]]

## Текущее состояние на `2026-04-17`

- `Android TOTP-first` закрыт как локальный slice
- backend уже реализует `Device Registry`, device-bound `push approve/deny` и delivery/outbox contour
- mobile уже имеет pending inbox shell, secure device session storage и runtime transport для `activate/refresh/pending/approve/deny`
- `Iteration 4` закрыла последний mobile gap для `push` runtime: approve теперь проходит через локальный biometric gate, а базовая история решений хранится локально в sanitized encrypted store без transport-данных

## Scope текущего трека

Входит:

- device-authenticated read path для pending `push` challenge
- mobile runtime screen для pending `push` approvals
- device token/session lifecycle для Android клиента
- local biometric gate перед `approve`
- базовая история последних решений на устройстве

Не входит:

- `iPhone`
- mandatory attestation
- installer/setup-plane логика внутри mobile app
- self-service enrollment
- provider admin console или внешняя delivery observability UI

## Итерации

### Iteration 1. Pending inbox contract foundation

Status:

- completed on `2026-04-17`

Цель:

- сначала стабилизировать backend/device contract для pending `push`, а уже потом строить mobile polling и UI

Выход:

- backend публикует `GET /api/v1/devices/me/challenges/pending` под `DeviceBearer`
- read path возвращает только active pending `push` challenges, уже bound к authenticated device
- contract остается sanitized: без `pushToken`, без raw callback secrets, без произвольных operator notes
- `OpenAPI`, `Auth and Token Flows`, `Device Lifecycle Design`, `Current State`, `Implementation Map` и session log синхронизированы
- automated tests закрывают handler + endpoint-level filter/scope behavior

Не входит:

- Android network client
- polling
- biometric prompt
- production token storage на устройстве

### Iteration 2. Mobile push feature shell

Status:

- completed on `2026-04-17`

Цель:

- добавить в `mobile` отдельный feature-модуль для pending `push` approvals поверх testable presenter/workflow contracts

Выход:

- новый `:feature:push-approvals`
- empty/runtime UI state для pending challenge cards
- injected read-model и decision callbacks без прямого знания про HTTP/token storage
- unit tests для presenter/workflow
- `AuthenticatorApp` wired к shell через injected `pendingPushApprovals` и decision callbacks без transport/storage coupling
- локальная проверка проходит: `:feature:push-approvals:testDebugUnitTest`, `:app:testDebugUnitTest`, `:app:assembleDebug`

### Iteration 3. Device session and decision transport

Status:

- completed on `2026-04-17`

Цель:

- связать mobile shell с runtime backend contour без fail-open хранения device credentials

Выход:

- secure client-side storage для device token pair и installation identity
- transport layer для `activate`, `refresh`, `GET /api/v1/devices/me/challenges/pending`, `approve`, `deny`
- controlled refresh path и fail-closed handling expired/blocked/revoked device state
- app wiring и unit tests на transport/domain adapter layer
- локальная проверка проходит: `:security:storage:testDebugUnitTest`, `:feature:push-approvals:testDebugUnitTest`, `:app:testDebugUnitTest`, `:app:assembleDebug`
- live Android verification подтверждена на `emulator-5554`: приложение стартует, pending section рендерится, package остается в foreground, а `logcat` не показывает runtime errors

### Iteration 4. Biometric gate and live closure

Status:

- completed on `2026-04-17`

Цель:

- довести `push` runtime до минимального operator/user-facing DoD

Выход:

- local biometric gate перед `approve`
- базовая история последних решений без хранения чувствительных transport secrets
- instrumented tests и live verification через доступный Android tooling/MCP
- vault sync и финальный checkpoint для следующего context reset

Фактическое закрытие:

- `mobile/app` теперь использует отдельный decision coordinator, который оборачивает approve в `BiometricPrompt` и пишет только sanitized local history
- `mobile/security:storage` получил encrypted `SecurePushDecisionHistoryStore` с limit/trim semantics и fail-closed corrupted-record handling
- `mobile/feature:push-approvals` теперь рендерит не только pending inbox, но и локальную историю решений и принимает typed decision results вместо blind exception-only flow
- локальная проверка проходит: `:security:storage:testDebugUnitTest :feature:push-approvals:testDebugUnitTest :app:testDebugUnitTest` зеленые
- instrumented contour проходит: `:app:connectedDebugAndroidTest` зеленый на `emulator-5554`
- live MCP verification на установленном `app-debug.apk` подтверждает render нового push section/historical empty state на `emulator-5554`

## Почему порядок именно такой

- read contract должен стабилизироваться раньше mobile UI, иначе feature начнет жить на временном API
- polling/session transport должен появиться раньше biometrics, иначе biometric gate будет оборачивать еще неустойчивый runtime path
- provider-specific delivery adapter остается отдельным backend track и не блокирует локальный mobile runtime, если pending inbox уже можно читать polling-ом

## Checkpoint for Context Reset

Если сессия очищена после `Iteration 1`, продолжать так:

1. читать `OTP/01 - Current State.md`
2. читать `OTP/Agent/Implementation Map.md`
3. читать эту заметку
4. проверить `backend/OtpAuth.Api/Endpoints/DevicesEndpoints.cs` и `backend/OtpAuth.Application/Challenges/ListPendingPushChallengesForDeviceHandler.cs`
5. `Iteration 1-4` считать закрытыми и не возвращаться к уже реализованным mobile transport/biometric/history slices без нового product decision
