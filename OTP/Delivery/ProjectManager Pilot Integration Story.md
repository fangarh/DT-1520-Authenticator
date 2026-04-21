# ProjectManager Pilot Integration Story

## Status

Accepted working note

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

Этот roadmap должен жить в репозитории `ProjectManager`, а данный note остается source of truth для самого pilot story в `OTP/`.
