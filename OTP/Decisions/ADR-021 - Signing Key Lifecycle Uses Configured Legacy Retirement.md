# ADR-021: Signing Key Lifecycle Uses Configured Legacy Retirement

## Status

Accepted

## Context

Bootstrap `OAuth` уже умеет:

- выпускать JWT access token с `kid`
- валидировать подпись по current + legacy signing keys

Но без отдельного lifecycle-инварианта остаются security-риски:

1. legacy signing key может оставаться валидным бесконечно долго;
2. rollout нового current key легко сделать, но retirement старого key остается ручным и плохо наблюдаемым;
3. хранить signing keys в `PostgreSQL` нельзя, потому что это повышает blast radius и смешивает config secret management с runtime data store.

Для `MVP` нужен operational workflow, который остается config-driven, не требует admin API и не заставляет хранить signing material вне secret manager/env-config.

## Decision

- signing keys остаются внешней конфигурацией, а не persistent data model в `PostgreSQL`
- current signing key задается через `BootstrapOAuth__CurrentSigningKeyId` и `BootstrapOAuth__CurrentSigningKey`
- предыдущий current key при rollout переводится в `BootstrapOAuth__AdditionalSigningKeys__{n}`
- для legacy signing key вводится `RetireAtUtc`
- runtime validation принимает legacy key только пока `RetireAtUtc` не наступил
- если `kid` присутствует, но key уже retired или отсутствует в активном key ring, validation работает по `fail-closed`
- вне `Development` запрещается process-local ephemeral signing key
- operational inspection lifecycle выполняется через отдельную команду migration runner-а, а не через HTTP management API

## Consequences

- rollout нового signing key выполняется без немедленного обрыва уже выданных short-lived JWT
- retirement legacy key становится явным и может происходить автоматически по timestamp в конфигурации
- signing key secrets не попадают в БД и не требуют отдельной схемы хранения в bootstrap-контуре
- операционный runbook обязан задавать `RetireAtUtc` как минимум на `access token lifetime + clock skew` после rollout
- при необходимости emergency retirement legacy key можно ускорить простым изменением конфигурации и redeploy
