# Admin UI MVP Plan

## Status

Partially implemented working guideline

## Цель

Разложить первый `Admin UI MVP` на конкретные экраны, frontend-модули, backend-зависимости, тестовый контур и security gates так, чтобы реализация шла поверх уже стабилизированного backend lifecycle для `TOTP` enrollment без installer-логики и без расползания ответственности.

## Scope

`Admin UI MVP` покрывает только runtime-операции операторского `TOTP` enrollment management:

- просмотр текущего enrollment state
- запуск `TOTP` enrollment
- подтверждение enrollment кодом
- безопасный `replace`
- `revoke`

## Текущее состояние реализации

Уже реализовано:

- отдельный browser auth contour через `GET/POST /api/v1/admin/auth/*` с cookie session и `CSRF`
- current enrollment lookup по `tenantId + externalUserId`
- admin command transport для `start/confirm/replace/revoke`
- frontend runtime shell в `admin` с `LoginPanel`, enrollment workspace, status card и action panels
- typed API client с `credentials: include`, CSRF bootstrap и problem mapping
- transport-error handling для browser fetch failures и path encoding для admin command calls
- unit tests для problem mapping и provisioning artifact parsing/discard

Пока остается закрыть:

- только косметический browser noise уровня `favicon.ico`, который не влияет на operator flow и не блокирует regression contour

## Out of scope

- installer/setup plane
- запуск или перезапуск backend/runtime units
- `Docker Compose`, `Helm`, host lifecycle management
- device lifecycle
- `push approval`
- `backup codes`
- integration client management UI
- чтение полного security audit trail как отдельного продукта

## Опорные backend-контракты

Текущий `Admin UI MVP` строится поверх уже реализованных endpoint-ов:

- `GET /api/v1/enrollments/totp/{enrollmentId}`
- `POST /api/v1/enrollments/totp`
- `POST /api/v1/enrollments/totp/{enrollmentId}/confirm`
- `POST /api/v1/enrollments/totp/{enrollmentId}/replace`
- `POST /api/v1/enrollments/totp/{enrollmentId}/revoke`

Текущий shape ответа для UI:

- `enrollmentId`
- `status`
- `hasPendingReplacement`
- `secretUri`
- `qrCodePayload`

Security contract backend-а уже зафиксирован:

- канонический reference для `secretUri`/`qrCodePayload`: [[../Integrations/TOTP Provisioning Contract]]
- `secretUri` и `qrCodePayload` возвращаются только в `start` и `replace`
- `read`, `confirm` и `revoke` не должны повторно возвращать provisioning artifacts
- конфликты и ошибки уже покрыты endpoint-level tests

## Основные пользовательские сценарии

### 1. Start enrollment

Оператор:

- вводит `tenantId`
- при неоднозначном tenant-е вводит `applicationClientId`
- вводит `externalUserId`
- опционально задает `issuer`
- опционально задает `label`
- получает provisioning artifact

UI должен:

- показать `QR` и manual provisioning string только один раз
- сохранить `enrollmentId` в локальном состоянии экрана
- не пытаться повторно получить секреты через read endpoint
- если backend возвращает `409` из-за нескольких active application clients у tenant-а, запросить у оператора явный `applicationClientId`

### 2. Confirm enrollment

Оператор:

- вводит код из authenticator app
- подтверждает enrollment

UI должен:

- показывать текущее состояние `pending -> confirmed`
- корректно обрабатывать `409`, если enrollment уже подтвержден или попытки исчерпаны
- явно различать restart enrollment и обычную ошибку ввода кода

### 3. Read current enrollment state

Оператор:

- открывает карточку enrollment-а
- видит `status` и `hasPendingReplacement`

UI должен:

- никогда не ожидать возвращения `secretUri` или `qrCodePayload` из read path
- безопасно отображать `pending`, `confirmed`, `revoked`
- показывать replacement badge по `hasPendingReplacement`

### 4. Replace enrollment

Оператор:

- инициирует replace у уже подтвержденного enrollment-а
- получает новый provisioning artifact
- подтверждает replacement кодом

UI должен:

- объяснять, что старый фактор остается активным до подтверждения replacement
- показывать новый `QR` только в ответе на `replace`
- после confirm обновлять состояние до `confirmed` без `pending replacement`

### 5. Revoke enrollment

Оператор:

- инициирует revoke
- подтверждает destructive action

UI должен:

- требовать явное подтверждение
- после revoke переводить экран в `revoked`
- скрывать любые старые provisioning artifacts

## Предлагаемые экраны `MVP`

### Enrollment workspace

Один рабочий runtime-экран с тремя зонами:

- поиск или идентификация enrollment-а
- карточка текущего состояния
- action panel для `start`, `confirm`, `replace`, `revoke`

### Start panel

Поля:

- `tenantId`
- `externalUserId`
- `issuer`
- `label`

Состояния:

- idle
- submitting
- success with provisioning artifact
- conflict
- policy denied
- access denied

### Confirm panel

Поля:

- `enrollmentId`
- `code`

Состояния:

- pending confirmation
- submitting
- confirmed
- invalid code
- attempt limit reached
- no longer pending

### Enrollment status card

Показывает:

- `enrollmentId`
- `status`
- `hasPendingReplacement`
- пояснение по доступным действиям

Не показывает:

- `secretUri`
- `qrCodePayload`

### Replace action

Поведение:

- доступна только для `confirmed`
- открывает отдельный replacement flow
- показывает новый provisioning artifact только в рамках replacement session

### Revoke dialog

Поведение:

- destructive confirmation
- отдельный текст риска
- явное подтверждение перед `POST /revoke`

## Backend gaps перед полноценной UI-реализацией

### Принятый auth contour

Для `Admin UI MVP` принят отдельный `admin auth contour`.

Следствия:

- browser UI не использует integration `client_credentials`
- admin session строится как human-operator session model
- admin role/permission boundary отделяется от external integration client boundary

### Принятая enrollment lookup/read model

Для operator UX принят отдельный current-enrollment read model по:

- `tenantId`
- `externalUserId`

Следствия:

- `Admin UI` не строится вокруг одного `enrollmentId`
- нужен admin-facing read endpoint для current `TOTP` enrollment summary пользователя
- `secretUri` и `qrCodePayload` не входят в lookup/read model

### P1. Problem mapping для UI

Нужно зафиксировать маппинг backend `Problem`-ответов в стабильные пользовательские сообщения и action hints.

## Предлагаемая frontend-структура

### App shell

- `admin/src/app/`
- router
- auth/session bootstrap
- runtime configuration

### Feature modules

- `admin/src/features/enrollment-workspace/`
- `admin/src/features/enrollment-start/`
- `admin/src/features/enrollment-confirm/`
- `admin/src/features/enrollment-status/`
- `admin/src/features/enrollment-replace/`
- `admin/src/features/enrollment-revoke/`

### Shared modules

- `admin/src/shared/api/`
- `admin/src/shared/problem/`
- `admin/src/shared/ui/`
- `admin/src/shared/config/`
- `admin/src/shared/types/`

## TypeScript contracts для frontend

Минимальные модели:

- `TotpEnrollmentStatus = 'pending' | 'confirmed' | 'revoked'`
- `TotpEnrollmentView`
- `StartTotpEnrollmentRequest`
- `ConfirmTotpEnrollmentRequest`
- `ProblemDetails`

`TotpEnrollmentView` должен содержать:

- `enrollmentId`
- `status`
- `hasPendingReplacement`
- `secretUri?`
- `qrCodePayload?`

На клиенте нужно отдельно различать:

- response, который может содержать provisioning artifact
- response, который по security contract не должен его содержать

## UI state machine

### Enrollment lifecycle

- `unknown`
- `pending`
- `confirmed`
- `revoked`

### Provisioning artifact visibility

- `hidden`
- `visible-for-current-start-or-replace-session`
- `discarded`

Правило:

- после ухода со страницы, reload или перехода в read flow provisioning artifact считается discarded
- повторное получение секрета через read path не допускается

## Тестовый контур

### Unit tests

- маппинг `Problem -> UI message`
- state transitions для `start`, `confirm`, `replace`, `revoke`
- masking и discard логики provisioning artifact

Текущий статус:

- реализованы `admin/src/shared/problem/problem-messages.test.ts`
- реализованы `admin/src/features/enrollment-workspace/model/provisioning-artifact.test.ts`
- реализованы `admin/src/shared/api/admin-api.test.ts`
- реализованы component tests `admin/src/features/auth/LoginPanel.test.tsx`
- реализованы component tests `admin/src/features/enrollment-status/EnrollmentStatusCard.test.tsx`
- реализованы component tests `admin/src/features/enrollment-workspace/EnrollmentPanels.test.tsx`
- orchestration hook `useEnrollmentWorkspace` пока покрыт косвенно через helper/component tests, но не имеет отдельного hook-level harness

### Component tests

- `StartTotpEnrollmentForm`
- `ConfirmTotpEnrollmentForm`
- `EnrollmentStatusCard`
- `ReplaceEnrollmentFlow`
- `RevokeEnrollmentDialog`

### E2E / Playwright

- `start -> show QR -> confirm -> confirmed`
- `replace -> show new QR -> confirm -> confirmed without pending replacement`
- `revoke -> revoked`
- `401/403/409` сценарии
- проверка, что read screen не показывает provisioning artifact

Текущий статус:

- browser verification была повторно попытана на `2026-04-15`
- `Vite` dev server на `http://127.0.0.1:4173` отвечает `200`
- после restart `Codex`/`Playwright MCP` live browser verification прошла успешно против реального `OtpAuth.Api` на `127.0.0.1:5112` и `dt-auth`
- подтверждены browser flows `logout/login`, `start -> confirm -> confirmed`, `replace -> confirm -> confirmed`, `revoke -> revoked` и follow-up `Load current`
- checked-in `Playwright` suite теперь прогоняет scripted browser regression поверх mocked `/api/v1/admin/*` contract без внешних секретов и отдельно подтверждает discard provisioning artifact после `reload/load current`
- regression suite больше не заблокирована `Transport closed`; оставшийся browser noise ограничен косметическим `404` на `favicon.ico`

## Security gates для `Admin UI MVP`

- `Admin UI` не получает host-level lifecycle прав
- runtime UI не пытается запускать backend, worker или миграции
- provisioning artifacts не сохраняются в `localStorage`, query string или постоянный клиентский кэш
- UI не показывает секреты повторно после `start`/`replace`
- destructive actions требуют подтверждения
- auth model и права доступа зафиксированы отдельно до начала UI implementation
- визуальные изменения проверяются через `Playwright`

## Рекомендуемый порядок реализации

1. Реализовать backend `admin auth contour`. Выполнено.
2. Реализовать current-enrollment admin read endpoint по `tenantId + externalUserId`. Выполнено.
3. Собрать `Admin shell` и API client layer. Выполнено.
4. Реализовать `start + confirm`. Выполнено.
5. Добавить `read/status`. Выполнено.
6. Добавить `replace`. Выполнено.
7. Добавить `revoke`. Выполнено.
8. Закрыть component tests и `Playwright` regression suite. Выполнено: component tests закрыты через `Vitest`, а scripted browser regression оформлена в `admin/e2e`.

## Ожидаемый результат `MVP`

После завершения этого плана `Admin UI` должен позволять оператору безопасно пройти весь lifecycle `TOTP` enrollment management без installer-функций и без повторного раскрытия provisioning secrets.
