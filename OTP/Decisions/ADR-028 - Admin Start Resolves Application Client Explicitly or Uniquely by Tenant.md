# ADR-028: Admin Start Resolves Application Client Explicitly or Uniquely by Tenant

## Status

Accepted

## Context

`Admin UI MVP` уже использует отдельный `admin auth contour` и current enrollment read model по:

- `tenantId`
- `externalUserId`

Но `TOTP` enrollment start по-прежнему требует `applicationClientId` для:

- `PolicyContext`
- storage uniqueness по `(tenantId, applicationClientId, externalUserId)`
- audit payload и дальнейших enrollment lifecycle операций

При этом browser operator flow не должен зависеть от integration `client_credentials` и не должен тащить `IntegrationClientContext` как transport contract.

Также tenant может иметь:

- ни одного активного integration client-а
- ровно один активный integration client
- несколько активных integration clients

Нельзя молча выбирать произвольный `applicationClientId`, если контекст неоднозначен.

## Decision

- admin `start` принимает `applicationClientId` как optional поле request contract
- если `applicationClientId` передан, backend обязан проверить, что он принадлежит указанному `tenantId`
- если `applicationClientId` не передан, backend может auto-resolve его только когда для `tenantId` существует ровно один активный integration client
- если активных integration clients нет, backend возвращает `404`
- если активных integration clients больше одного и `applicationClientId` не передан, backend возвращает fail-closed `409`
- `confirm/replace/revoke` на admin contour не требуют `applicationClientId` в route или request body и используют отдельный server-side lookup enrollment-а по `enrollmentId`
- operator read model может возвращать `applicationClientId` как metadata для follow-up UI flows, но не раскрывает provisioning secrets

## Consequences

- admin transport остается отделенным от integration auth boundary
- backend не делает неявный и небезопасный выбор application client-а при многоклиентном tenant-е
- `Admin UI` может работать без дополнительного поля в простом single-client tenant сценарии
- при multi-client tenant-е UI должен запросить явный `applicationClientId`
- server-side admin command transport получает собственный by-id lookup path и не зависит от `IntegrationClientContext`
