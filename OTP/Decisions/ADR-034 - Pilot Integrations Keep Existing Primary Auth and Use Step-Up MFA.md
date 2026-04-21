# ADR-034 - Pilot Integrations Keep Existing Primary Auth and Use Step-Up MFA

## Status

Accepted

## Context

Для `Iteration 3 / Slice 3A` нужен один канонический pilot-grade integration story, который проверяет не только локальные runtime-слайсы `Authenticator`, но и реальный встраиваемый сценарий во внешнее приложение.

В качестве первого pilot-кандидата выбран `ProjectManager`, потому что он уже имеет рабочий `Keycloak + OIDC` контур, backend `JWT` validation и стабильную внешнюю identity через `Keycloak sub`.

Попытка заменить существующий primary auth flow на стороне pilot-приложения приведет к лишнему риску, расширению scope и смешению двух разных задач:

- замена существующего `IdP`
- добавление step-up `MFA` для чувствительных операций

Для `MVP` нам нужно доказать вторую задачу, а не первую.

## Decision

- pilot integrations не заменяют existing primary auth/`IdP`; существующий login flow остается source of truth для первичной аутентификации пользователя
- `DT-1520 Authenticator` в pilot используется как отдельный step-up contour для чувствительных операций
- canonical external identity для step-up берется из уже существующего stable subject внешнего приложения
- для `ProjectManager` canonical `externalUserId` равен `Keycloak sub`
- первый pilot target app фиксируется как `ProjectManager`
- первая pilot operation фиксируется как create/update `VCS instance` credentials, а не общий login flow и не весь admin surface сразу
- integration идет backend-to-backend через existing `OAuth 2.0 client_credentials` contour `Authenticator`, а не direct SPA-to-Authenticator calls
- primary completion signal для protected operation идет через signed `challenge callback`; polling `GET /api/v1/challenges/{id}` допускается только как resilience fallback для resumed session/recovery path
- `Task Tracker` credentials reuse-ят тот же pattern только после закрытия pilot на `VCS instance`

## Consequences

- pilot проверяет реалистичный enterprise сценарий: external `IdP` остается на месте, а `Authenticator` добавляет step-up `MFA` там, где это действительно нужно
- identity mapping упрощается: `ProjectManager` не должен изобретать новый user correlation layer поверх уже существующего `Keycloak sub`
- integration surface остается узкой и проверяемой: `ProjectManager` backend становится единственной точкой, которая общается с `Authenticator`
- первый pilot slice ограничивается server-side pending operation orchestration, callback handling и operator/mobile confirmation, без переработки существующего login UX
- future integrations с другими приложениями могут reuse-ить тот же pattern: keep existing primary auth, pass stable external subject, protect only sensitive operations
