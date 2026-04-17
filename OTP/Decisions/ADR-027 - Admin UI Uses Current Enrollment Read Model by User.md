# ADR-027: Admin UI Uses Current Enrollment Read Model by User

## Status

Accepted

## Context

Текущий enrollment read path в backend адресуется по `enrollmentId`:

- `GET /api/v1/enrollments/totp/{enrollmentId}`

Этого достаточно для API lifecycle и follow-up после `start`, но недостаточно для operator UX:

- оператор думает в терминах пользователя, а не `enrollmentId`
- повторный вход в сценарий неудобен, если известны только `tenantId` и `externalUserId`
- `Admin UI` не должен строиться вокруг случайного технического идентификатора, полученного в предыдущей сессии

Для первого `Admin UI MVP` не требуется полноценная история всех enrollment-ов пользователя, но нужен минимальный lookup/read contour.

## Decision

- `Admin UI MVP` получает отдельный read model для current `TOTP` enrollment по пользователю
- канонический lookup для operator UX строится по:
  - `tenantId`
  - `externalUserId`
- backend должен предоставить admin-facing read endpoint, возвращающий current enrollment summary пользователя без provisioning artifacts
- read model должен возвращать минимум:
  - `enrollmentId`
  - `status`
  - `hasPendingReplacement`
  - metadata, достаточную для operator decisions
- `secretUri` и `qrCodePayload` не входят в этот read model
- by-id read endpoint остается техническим/внутренним и может использоваться для follow-up flows, но не является основой operator lookup UX

## Consequences

- `Admin UI` становится пригодным для реальной операторской работы, а не только для линейного happy-path после `start`
- backend получает отдельный admin-facing read contract
- первая версия UI может обойтись без полной истории enrollment-ов, ограничившись current state view
- security boundary улучшается: read model по пользователю не переиспользует provisioning response и не создает соблазн повторно раскрывать secrets через read path
