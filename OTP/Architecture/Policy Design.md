# Policy Design

## Status

Draft

## Цель

Зафиксировать рабочий контракт модуля `Policy` для `MVP`, чтобы backend-реализация не принимала policy-решения в контроллерах, `Challenge`-логике или UI.

## Роль модуля

`Policy` определяет:

- нужен ли второй фактор для конкретного сценария
- какие факторы разрешены в текущем контексте
- разрешен ли `push`
- допустим ли fallback на `TOTP`
- допустим ли конкретный enrollment action

## Security principles

- `deny by default`, если контекст неполный
- policy не доверяет UI и клиентским подсказкам как источнику истины
- если фактор не разрешен явно, он запрещен
- policy result должен быть explainable и auditable
- security-critical allow/deny должен фиксироваться в audit trail

## PolicyContext

Минимальный входной контракт для `MVP`:

- `tenantId`
- `applicationClientId`
- `operationType`
- `userId`
- `userStatus`
- `requestedFactor`
- `availableFactors`
- `deviceTrustState`
- `deploymentProfile`
- `environmentMode`
- `challengePurpose`
- `enrollmentInitiationSource`
- `pushChannelAvailable`

## PolicyContext value semantics

### `operationType`

- `login`
- `step_up`
- `device_activation`
- `totp_enrollment`
- `backup_code_recovery`

### `deviceTrustState`

- `none`
- `pending`
- `active`
- `revoked`
- `blocked`

### `deploymentProfile`

- `cloud`
- `on_prem`
- future: `air_gapped`

### `enrollmentInitiationSource`

- `admin`
- `trusted_integration`
- `self_service`

## PolicyDecision

Минимальный выходной контракт для `MVP`:

- `requiresSecondFactor`
- `allowedFactors`
- `preferredFactor`
- `pushAllowed`
- `totpAllowed`
- `backupCodeAllowed`
- `enrollmentAllowed`
- `denyReason`
- `auditReason`
- `evaluationTrace`

## Правила формирования решения

- `PolicyDecision` не должен быть простым `bool`
- наружу не нужно отдавать всю внутреннюю механику evaluation
- `evaluationTrace` нужен для диагностики, тестов и аудита, а не как публичный API contract

## MVP ruleset

### 1. Factor availability rules

- policy определяет, какие факторы вообще доступны для tenant, client и user
- запрещенные или неинициализированные факторы не попадают в `allowedFactors`

### 2. Deployment rules

- если deployment profile не допускает внешний `push`, то `pushAllowed = false`
- `on_prem` не предполагает обязательную доступность `APNs/FCM`
- future `air_gapped` profile должен быть совместим с `TOTP` и `backup codes`

### 3. Device trust rules

- `push` разрешен только при `deviceTrustState = active`
- `revoked` и `blocked` устройства всегда запрещены для approve flow
- `pending` устройство не может быть trusted factor для `push approval`

### 4. Enrollment rules

- enrollment инициируется только через admin-led или trusted integration flow
- self-service enrollment не входит в `MVP`
- policy должна уметь явно запретить неразрешенный enrollment action

### 5. Fallback rules

- если `push` недоступен, policy может вернуть fallback на `TOTP`
- `backup codes` не должны автоматически считаться равнозначной заменой для всех операций

### 6. Challenge rules

- для security-sensitive операций policy должна требовать второй фактор, даже если клиент пытается его опустить
- policy должна учитывать `operationType` и `challengePurpose`, а не только requested factor

## MVP behavior matrix

### `login`

- разрешены `push` или `totp`
- `push` только при `active` device и допустимом deployment profile

### `step_up`

- разрешены `push` или `totp`
- `backup code` по умолчанию не является preferred factor

### `device_activation`

- идет через отдельный activation/enrollment flow
- не должен восприниматься как обычный `push approval`

### `totp_enrollment`

- разрешен только при admin-led initiation

### `on_prem` без доступного `push`

- разрешены `totp` и, при явном policy-разрешении, `backup codes`

## Backend mapping

### `OtpAuth.Application`

- `PolicyContext`
- `PolicyDecision`
- `IPolicyEvaluator`

### `OtpAuth.Domain`

- доменные policy rules
- value objects для factor availability, trust state и operation type

### `OtpAuth.Infrastructure`

- загрузка policy-конфигурации tenant/client
- адаптация deployment profile и feature flags
- реализация `DefaultPolicyEvaluator`

### `OtpAuth.Api`

- сбор входного context
- вызов evaluator
- отсутствие самостоятельных policy-решений в controller/endpoint code

## Текущее bootstrap implementation решение

Пока полноценные `User`, `Enrollment` и `Device Registry` еще не реализованы, первый `CreateChallenge` slice собирает policy context консервативно:

- `pushChannelAvailable = false`
- `deviceTrustState = none`
- `deploymentProfile = cloud`
- `availableFactors` берутся из `preferredFactors`, но итоговый `push` все равно запрещается policy-слоем без trusted device context

Это дает безопасное поведение по умолчанию: без фактического device context `push` не включается в критический путь.

## Базовый интерфейс

```csharp
public interface IPolicyEvaluator
{
    PolicyDecision Evaluate(PolicyContext context);
}
```

## Чего не делать в `MVP`

- не делать внешний policy service
- не делать произвольный admin rule builder
- не делать JSON DSL для правил
- не принимать factor decision прямо в controller без вызова `Policy`
- не смешивать policy evaluation с transport-level authorization middleware

## Следующий шаг

После этой заметки нужно:

1. перенести `PolicyContext` и `PolicyDecision` в канонический backend namespace design
2. описать первый набор use cases, которые обязаны вызывать `Policy`
3. расширить unit и integration tests для policy evaluation
