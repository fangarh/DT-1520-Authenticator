# ADR-036 - Optional Boxed Integration Gateway and Access Connector Boundary

## Status

Accepted

## Context

Reference `Desktop + Backend` stand proved that desktop and legacy application integrations need a backend-owned contour: the desktop side must not keep `integration client_secret`, and public callbacks must not depend on third-party tunnels such as `trycloudflare`.

For product delivery this means the reference backend should not remain only a demo. Many customers will need a deployable boxed component that can live in their own infrastructure and bridge legacy applications, desktop clients, internal services and DT-1520 Authenticator.

There is also an adjacent enterprise scenario: `TOTP` during `RDP` or Windows access. This is not the same integration surface as application step-up approval. `RDP` normally belongs to an access-control connector layer such as `RADIUS/NPS`, `RD Gateway` integration, VPN/RADIUS integration, or later a Windows Credential Provider. If we ignore this now, the gateway and SDK boundaries can accidentally become too desktop-HTTP-specific and force rewrites before production access scenarios.

## Decision

Adopt `Optional Boxed Integration Gateway` as the recommended productization direction after the reference stand.

The gateway is an optional first-class deployable component, not a required cloud dependency and not only a sample. It should be suitable for customer-owned deployment together with the core server or near the integrated legacy system.

The gateway owns integration orchestration around MFA operations:

- creating approval/TOTP verification sessions
- calling DT-1520 Authenticator through backend-only credentials
- validating signed callbacks
- exposing polling/status endpoints to desktop or legacy clients
- providing online `TOTP` fallback through the central server
- publishing diagnostics, readiness and latency measurements

The gateway must not become a generic workflow engine and must not own the customer's business transaction. The integrating application remains responsible for deciding what operation is protected and for committing or rolling back its own business changes.

Keep shared lower-level services reusable by future access connectors:

- integration token acquisition and DT-1520 client calls
- challenge/session creation
- callback signature validation
- online `TOTP` verification
- device lookup/routing when explicitly needed
- audit, diagnostics and failure mapping

Do not bake desktop-only assumptions into domain/application services. Transport-specific details such as HTTP polling, desktop session URLs or WPF shell behavior must stay at the gateway/adapter edge.

Treat `RDP + TOTP` as a future production access connector track, not as part of the application integration gateway itself. The recommended first production direction for that track is `RADIUS/NPS` with `TOTP-first`; push approval can be added later where timeout and UX constraints allow it. Windows Credential Provider remains a later, heavier connector option.

Future architecture and code decisions must preserve compatibility with the access connector track:

- short authentication timeouts
- fail-closed policy and explicit recovery/break-glass operations
- mapping `AD`/directory usernames to canonical `externalUserId`
- no browser/desktop polling dependency in the access path
- no customer business transaction commit inside the connector
- central audit, replay defense, rate limiting and revocation checks

## Consequences

`rdb_stand/src/ReferenceBackend` can evolve toward the boxed gateway, but must remain split so reusable SDK/application pieces are not coupled to a specific desktop demo.

The server-owned `ghostring` reference-backend contour is accepted as a stepping stone toward this gateway: it removes third-party callback tunnels and proves customer-owned hosting, callback ingress and private DT-1520 connectivity.

The production roadmap must include a separate `RDP + TOTP` access connector design before production hardening. It should be considered when changing challenge contracts, identity mapping, policy, callback/session models, audit events, device routing and SDK package boundaries.

The gateway is useful, but optional: direct customer backend integration through REST/SDK remains valid for teams that already have a backend service and do not need a boxed bridge.
