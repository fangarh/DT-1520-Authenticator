# Admin Client Management Follow-Up

## Status

Accepted mandatory follow-up task

## Trigger

Эта задача становится обязательной сразу после прохождения первого ручного pilot-теста `ProjectManager -> DT-1520`.

Ее нельзя считать optional cleanup или nice-to-have. Это обязательный следующий productization step после подтверждения ручного happy path.

## Goal

Довести `Admin UI` и backend admin contour до полноценной возможности подключения внешних клиентов без ручных bootstrap workaround-ов.

Под "полноценной возможностью подключения клиентов" здесь понимается не read-only visibility, а полный operator-ready lifecycle integration client-а.

## Why this is mandatory

Текущий pilot можно пройти через server-side/manual preparation, но это не считается достаточным operator-ready solution.

После первого ручного pilot-теста система должна уметь штатно:

- создать новый integration client
- выдать plaintext secret в момент создания
- назначить/изменить scopes
- показывать tenant/application binding
- безопасно ротировать secret
- деактивировать/reactivate client
- давать operator/support понятный и воспроизводимый client onboarding flow

Без этого продукт остается pilot-capable, но не operator-complete для подключения новых внешних систем.

## Required outcome

После закрытия этой задачи `Admin UI` и backend должны покрывать как минимум:

- create integration client
- one-time display plaintext client secret
- read/list integration clients
- view current metadata:
  - `clientId`
  - `tenantId`
  - `applicationClientId`
  - allowed scopes
  - lifecycle status
- rotate secret
- deactivate/reactivate client
- sanitized audit trail для admin client actions

## Security requirements

- plaintext secret показывается только в creation/rotation moment
- secret не попадает в read models, logs, audit payloads или browser persistence
- все state-changing admin actions идут под current admin auth contour и `CSRF`
- lifecycle actions остаются fail-closed
- operator не может случайно получить access к чужим tenant/application bindings вне allowed scope

## Not enough to close this task

Следующее не считается достаточным закрытием:

- только bootstrap script
- только CLI/manual seed path
- только read-only client list
- только docs/runbook без actual admin workflow

## Continuation point

Выполнять эту задачу только после подтвержденного первого ручного pilot-теста.

После ручного pilot-теста это должен быть следующий обязательный admin/productization track.
