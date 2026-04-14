# Week 1 Execution Plan

## Цель недели

Перейти от общего проектного каркаса к состоянию, в котором можно безопасно запускать первый backend vertical slice.

## День 1

- утвердить `ADR-011 - MVP Tenancy Model`
- утвердить `ADR-012 - Admin-Led Enrollment for MVP`
- утвердить `ADR-013 - Device Trust Lifecycle Without Mandatory Attestation in MVP`
- зафиксировать стратегическое направление `IdP` и optional `push` profile для `on-prem`

Выход:

- закрыты решения, влияющие на `Tenant`, `User`, `ApplicationClient`, `Device`, `Challenge`
- зафиксированы продуктовые и security boundaries для `IdP`-траектории и `on-prem`

## День 2

- детализировать [[../Architecture/Backend Module Design]]
- разложить первый `MVP` slice на use cases

Выход:

- есть список application handlers, доменных сущностей и событий

## День 3

- довести [[../Security/Security Model MVP]] до согласованного baseline
- согласовать storage, token lifecycle, revoke semantics и rate limits

Выход:

- security-модель больше не остается подразумеваемой

## День 4

- оформить [[Testing Strategy]]
- описать `Persistence and Migrations Plan`

Выход:

- есть критерии `Definition of Done`
- есть стартовая persistence-рамка для backend

## День 5

- сформировать backlog реализации первого vertical slice
- привязать к `OpenAPI`, use cases, persistence и тестам

Выход:

- готовы задачи на `CreateChallenge`, `GetChallengeStatus`, `VerifyTotp`

## Security-first правило недели

Ни одна задача не считается готовой к реализации, если для нее не определены:

- auth model
- validation boundary
- storage policy для чувствительных данных
- audit event
- test cases на отрицательные сценарии
