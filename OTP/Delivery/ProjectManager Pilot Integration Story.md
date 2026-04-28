# ProjectManager Pilot Integration Story

## Status

Accepted working note

`ProjectManager`-side implementation completed, live Authenticator device path verified, first ProjectManager-created manual pilot completed with a delivery latency residual

## Goal

Зафиксировать канонический `Iteration 3 / Slice 3A` scenario для `MVP Closure` поверх уже закрытых runtime/admin/device контуров `Authenticator`.

Этот pilot должен доказать, что `DT-1520 Authenticator` можно встроить в существующее корпоративное приложение без замены его текущего primary auth flow.

## Chosen Pilot

Выбранное pilot-приложение: `ProjectManager`

Выбранная первая protected operation:

- create/update `VCS instance` credentials

Первый pilot intentionally не включает:

- замену `Keycloak`
- защиту всего login flow
- одновременное покрытие `Task Tracker` и `VCS`
- массовое включение step-up на все write endpoints подряд

## Why ProjectManager

`ProjectManager` уже имеет:

- primary auth через `Keycloak + OIDC Authorization Code + PKCE`
- backend `JWT` validation для `project-manager-api`
- stable external subject через `Keycloak sub`
- чувствительную admin operation с credential-bearing payload в `VCS instance`

Это делает его сильнее pilot-кандидатом, чем локальные demo-приложения или приложения с полностью self-contained session auth.

## Canonical Identity Mapping

- primary identity source: `Keycloak`
- identity inside `ProjectManager`: `sub` claim, уже используемый в `JitProvisioningMiddleware`
- canonical `externalUserId` для `Authenticator`: тот же `Keycloak sub`
- отдельный mapping layer между `ProjectManager` user и `Authenticator` user в pilot не вводится

## Actors

- `Operator/Admin` в `ProjectManager`
- `Keycloak`
- `ProjectManager Frontend`
- `ProjectManager Backend`
- `DT-1520 Authenticator API`
- `DT-1520 Authenticator Worker`
- `Authenticator Android App`

## Canonical Story

### Pre-conditions

- пользователь успешно аутентифицирован в `ProjectManager` через `Keycloak`
- `ProjectManager` зарегистрирован в `Authenticator` как integration client с нужными scope
- у пользователя уже есть active device в `Authenticator`
- device умеет принимать `push` approval
- у `ProjectManager` есть reachable `HTTPS` callback endpoint для challenge outcome

### Happy Path

1. Пользователь логинится в `ProjectManager` через существующий `Keycloak` flow.
2. `ProjectManager` backend получает JWT и уже знает stable `sub` через existing auth contour.
3. Пользователь открывает форму create/edit `VCS instance`.
4. Пользователь вводит или меняет `BaseUrl`, `Username`, `Password`, `SkipSslValidation`.
5. При submit `ProjectManager` не сохраняет изменение сразу.
6. `ProjectManager` backend создает pending protected operation и вызывает `Authenticator`.
7. В `CreateChallenge` backend передает:
   - `externalUserId = Keycloak sub`
   - operation metadata для `vcs.instance.create` или `vcs.instance.update`
   - `callback.url` на собственный backend endpoint
   - optional `targetDeviceId`, если позже pilot потребует deterministic multi-device routing
8. `Authenticator` создает `push` challenge и доставляет его на active device пользователя.
9. Android app читает pending challenge через device runtime path и показывает approval UX.
10. Пользователь подтверждает challenge через biometric-gated `approve`.
11. `Authenticator` переводит challenge в terminal state `approved` и worker доставляет signed callback в `ProjectManager`.
12. `ProjectManager` валидирует callback signature/status, завершает pending protected operation и только после этого сохраняет `VCS instance`.
13. Пользователь видит успешное завершение операции в `ProjectManager`.
14. При необходимости оператор может проверить delivery/device side effects через existing `Admin UI` `Authenticator`.

## Implementation status

На стороне `ProjectManager` pilot MFA slice уже реализован.

Что закрыто в коде:

- `POST/PUT /api/vcs-instances` больше не сохраняют credential-bearing payload сразу
- backend создает pending protected operation и инициирует backend-only integration call в `DT-1520`
- apply-path стал callback-driven и idempotent: approved operation применяется ровно один раз
- frontend показывает pending approval UX и user-scoped resume path без прямых browser calls в `Authenticator`
- polling идет только через `ProjectManager` backend
- чувствительный `VCS` password больше не живет в React state дольше submit flow

Ключевые implementation entry points на стороне `ProjectManager`:

- `src/Server/ProjectManager.Server/Program.cs`
- `src/Server/ProjectManager.Server/Api/Vcs/VcsEndpoints.cs`
- `src/Logic/PM.Repository/ProtectedOperations/ProtectedOperationRepository.cs`
- `src/Logic/PM.Repository/Scripts/018_protected_operations_SAFE.sql`
- `src/Client/frontend/src/App.tsx`
- `src/Client/frontend/src/components/VcsInstanceFormModal.tsx`
- `src/Client/frontend/src/components/VcsApprovalPendingModal.tsx`

Проверка, уже подтвержденная в репозитории `ProjectManager`:

- `dotnet test src\Tests\ProjectManager.Server.Tests\ProjectManager.Server.Tests.csproj`
- `npm test` в `src/Client/frontend`
- `npm run build` в `src/Client/frontend`
- `dotnet build src\Server\ProjectManager.Server\ProjectManager.Server.csproj`
- `dotnet build src\Server\ProjectManager.Worker\ProjectManager.Worker.csproj`
- `dotnet build src\ProjectManager.slnx`

Закрытые security properties:

- backend-only integration с `Authenticator`
- fail-closed path при `0` или `>1` подходящих device
- `HMAC`-verification signed callback
- user-scoped status polling
- encrypted pending payload at rest
- transparent encryption для сохраненного `VCS` password

Live Authenticator/device verification на `2026-04-24`:

- `ProjectManager` integration client `otpauth-projectmanager` уже имеет scopes `challenges:read`, `challenges:write`, `devices:write`.
- `ProjectManager` pilot user использует canonical `externalUserId=f1d6afaa-8a5d-4fd3-9f75-0a5c0177df81`.
- Android emulator активирован как device `077d09f9-8637-4583-8864-9b29ced707b4` и проходит device lookup как active push-capable device.
- Synthetic live challenges, созданные напрямую через `DT-1520` contract, отображаются в Android `Push Approvals` UI.
- Android `deny` path подтвержден server-side через terminal state `denied`.
- Android `approve` path подтвержден server-side через `BiometricPrompt`/device credential и terminal state `approved` для challenge `a202ef93-c2f5-4645-80cf-06af37d1d86d`.
- Первый ручной прогон через реальный `ProjectManager` login и protected `VCS` operation завершился успешно: вход остается обычным `Keycloak` flow, step-up approval появляется только при защищенной операции, а подтверждение проходит через Android emulator.
- Наблюдаемый lag между запросом в `ProjectManager` и появлением challenge на Android emulator составляет около `~60s`; это зафиксировано как delivery/polling UX residual для следующего hardening шага.
- Offline verification не проводилась: этот checkpoint подтверждает только online/live contour через `ghostring`, `ProjectManager`, `Keycloak` и Android emulator; поведение при недоступной сети, недоступном `DT-1520` runtime или offline fallback отдельно не проверено.

Незакрытые operational остатки после первого ProjectManager-created live pilot:

- разобраться с observed `~60s` lag до появления challenge в Android UI: проверить worker enqueue/delivery timings, mobile foreground polling interval и текущий `PushDelivery:Provider=logging` profile
- отдельно решить, нужна ли offline/fallback проверка для pilot scope, и если да, оформить отдельный сценарий с ожидаемым fail-closed поведением
- зафиксировать expected manual pilot UX copy в `ProjectManager`, чтобы пользователь понимал, что step-up approval появляется на защищенной операции, а не на login
- перед продуктовым pilot решить, оставляем ли временный polling-only режим или подключаем real push provider для near-real-time delivery

Отдельный plan item:

- [[Push Delivery Latency Follow-Up]]

## Failure Handling

### User has no active device

- `ProjectManager` fail-closed не сохраняет `VCS instance`
- пользователь получает generic operator-facing message без internal transport detail
- support path идет через existing `Authenticator Admin UI`: enrollment/device lookup

### Push denied

- pending protected operation закрывается как denied
- изменение в `ProjectManager` не коммитится
- UI показывает typed business outcome, а не generic transport error

### Push expired

- pending protected operation закрывается как expired
- изменение не коммитится
- UI предлагает повторить attempt осознанно, а не silently replay request

### Callback not delivered

- primary flow по-прежнему считается callback-driven
- `ProjectManager` может использовать `GET /api/v1/challenges/{id}` как controlled recovery path для resumed session/operator retry
- отсутствие callback не должно приводить к fail-open сохранению данных

### Device revoked or blocked before approval

- challenge approval не завершается успешно
- protected operation в `ProjectManager` остается незавершенной и не применяется
- support path reuse-ит existing admin device workspace в `Authenticator`

## Repo Entry Points

### ProjectManager backend

- `src/Server/ProjectManager.Server/Program.cs`
- `src/Server/ProjectManager.Server/JitProvisioningMiddleware.cs`
- `src/Server/ProjectManager.Server/Api/Vcs/VcsEndpoints.cs`

### ProjectManager frontend

- `src/Client/frontend/src/App.tsx`
- `src/Client/frontend/src/components/VcsInstanceFormModal.tsx`
- `src/Client/frontend/src/api/apiFetch.ts`

### Authenticator backend/runtime

- `backend/OtpAuth.Api/Endpoints/ChallengesEndpoints.cs`
- `backend/OtpAuth.Api/Endpoints/DevicesEndpoints.cs`
- `backend/OtpAuth.Worker/*delivery*`
- `OTP/Integrations/Auth and Token Flows.md`

### Authenticator operator/mobile surfaces

- `admin/` device and delivery workspaces
- `mobile/app` push approval runtime

## Contract Boundaries

- primary auth остается в `ProjectManager/Keycloak`
- `Authenticator` не становится login provider для pilot-приложения
- `ProjectManager Frontend` не получает integration `client_secret`
- весь вызов `Authenticator` идет только из `ProjectManager Backend`
- callback endpoint принимает только signed outcomes и не раскрывает transport internals наружу
- sensitive `VCS` payload не должен жить в браузере дольше текущего submit flow и не должен commit-иться до `approved`

## Out of Scope

- step-up на обычный read-only UX
- защита всех `ProjectManager` write operations в первом же срезе
- `Task Tracker` credentials в первом pilot pass
- замена `Keycloak` или унификация auth systems
- self-service device/enrollment UX внутри `ProjectManager`

## Delivery Handoff

Для фактической реализации этого pilot в репозитории `ProjectManager` нужен отдельный roadmap, который раскладывает работу на:

- backend pending-operation orchestration
- integration client вызовы в `Authenticator`
- callback endpoint and signature validation
- frontend approval UX вокруг `VCS` form submit
- tests и security hardening

Этот roadmap живет в репозитории `ProjectManager`, а данный note остается source of truth для pilot story и текущего integration status в `OTP/`.
