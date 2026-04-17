# ADR-026: Admin UI Uses Separate Admin Auth Contour

## Status

Accepted

## Context

К моменту старта `Admin UI MVP` backend уже имеет рабочий bootstrap auth contour для integration clients:

- `OAuth 2.0 client_credentials`
- short-lived JWT access token
- `scope`-based access
- revocation, introspection и lifecycle invalidation

Этот контур подходит для machine-to-machine вызовов, но не для browser-based operator UI.

Если использовать его напрямую для `Admin UI`, возникают проблемы:

- `client_credentials` не предназначен для human operator session в браузере
- `client_secret` нельзя безопасно размещать в SPA
- trust boundary integration client и admin/operator смешиваются
- audit и authorization semantics для человека и для внешней системы становятся двусмысленными

`Admin UI MVP` при этом уже признан следующим delivery-приоритетом, поэтому auth contour должен быть принят до реализации UI.

## Decision

- `Admin UI` не использует integration `OAuth 2.0 client_credentials` contour
- для `Admin UI` вводится отдельный `admin auth contour`
- browser-based admin session строится на server-managed session через защищенную cookie, а не на bearer token в `localStorage`
- admin auth contour должен поддерживать:
  - operator login
  - operator logout
  - role/permission model минимум на уровне `enrollments.read` и `enrollments.write`
  - отдельный audit контур для operator actions
- `Admin UI` и `Admin API` становятся отдельным human-operator trust boundary внутри монолита
- integration auth остается только для внешних систем и trusted integration сценариев

## Consequences

- `Admin UI` не зависит от browser-hosted `client_secret`
- admin/operator access model можно усиливать независимо от integration auth
- появляется отдельный backend scope для `Admin API`, session management, cookie security и CSRF protection
- текущие integration enrollment endpoints могут остаться для trusted integration flow, но не являются канонической основой для browser admin UI
- реализация `Admin UI MVP` теперь требует backend admin auth slice, а не только frontend
