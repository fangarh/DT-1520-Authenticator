# Optional Boxed Integration Gateway

## Recommendation

Build an optional boxed `Integration Gateway` as the productized successor of the reference `Desktop + Backend` stand.

This is the preferred direction for customers that need a ready deployable bridge between DT-1520 Authenticator and desktop, legacy, intranet or thin-client applications. It should run in the customer's own infrastructure and avoid third-party tunnel services for callbacks.

## Positioning

The gateway is optional.

Use it when:

- the integrated application cannot safely hold `integration client_secret`
- there is no convenient backend layer owned by the customer application
- callback ingress, polling/status endpoints and diagnostics should be packaged together
- desktop or legacy clients need a stable local/backend facade
- the customer wants a box-ready deployment unit rather than writing all orchestration code

Do not require it when:

- the customer already has a backend and can use REST or the official `.NET` SDK directly
- the scenario is pure server-to-server integration with simple callback handling
- the scenario is Windows/RDP/VPN access control rather than application step-up

## Scope

The gateway should own MFA orchestration only:

- protected-operation session API
- DT-1520 client calls through backend-only credentials
- signed callback validation
- desktop/legacy polling endpoints
- online `TOTP` fallback
- readiness, diagnostics and latency telemetry
- safe failure mapping and operator-facing health information

It should not own:

- customer business transactions
- customer primary authentication
- customer authorization model
- generic workflow/BPM logic
- storage of customer business secrets

## Architecture Guardrails

Keep the core model protocol-oriented:

- `tenantId`
- `applicationClientId`
- canonical `externalUserId`
- operation metadata
- factor policy
- challenge/session state
- callback/polling result
- audit and diagnostics

Keep adapter details at the edge:

- desktop polling
- WPF/console UI behavior
- HTTP route shape for a specific sample
- deployment-specific callback host/port
- customer-specific username mapping

Shared services must stay reusable by future access connectors. In particular, `TOTP` verification, challenge creation, token acquisition, callback validation, device lookup and audit should not depend on desktop session concepts.

## RDP and TOTP Constraint

`TOTP` for `RDP` is a separate future access connector track.

The likely first production option is `RADIUS/NPS` with `TOTP-first`, because it maps better to existing enterprise access infrastructure. Push approval can follow later if timeout and UX constraints are acceptable. Windows Credential Provider is more invasive and should be treated as a later connector, not as the first default.

Future gateway and SDK work must keep this scenario in mind:

- `RDP` flows have short timeouts.
- The connector must fail closed by default.
- Directory username mapping to `externalUserId` must be explicit.
- The connector cannot rely on browser or desktop polling UX.
- Central audit, replay defense, rate limiting and revocation must remain enforced by DT-1520.

## Near-Term Path

1. Keep the current `ReferenceBackend` as the proving ground.
2. Run it through the server-owned `ghostring` contour without third-party tunnels.
3. Extract stable gateway contracts from the stand after live push/TOTP proof.
4. Productize deployment, configuration, diagnostics and docs.
5. Before production access scenarios, design the separate `RDP + TOTP` connector boundary and confirm no gateway assumptions block it.

## Related Decisions

- [[../Decisions/ADR-035 - Official Dotnet Integration SDK and Reference Stand]]
- [[../Decisions/ADR-036 - Optional Boxed Integration Gateway and Access Connector Boundary]]
