# ADR-006: OAuth2 for Integrations and Device Tokens for Mobile

## Status

Accepted

## Context

Интеграционные клиенты и мобильные устройства обращаются к платформе в разных trust-моделях и с разным жизненным циклом учетных данных.

## Decision

- для интеграционных клиентов использовать `OAuth 2.0 client credentials`
- для мобильных устройств использовать отдельные bearer tokens, выдаваемые при активации устройства и обновляемые через refresh flow

## Consequences

- в контракте появляется `/oauth2/token`
- у операций можно задавать scope-ы
- lifecycle токенов устройства становится частью `Device Registry` и mobile security model
