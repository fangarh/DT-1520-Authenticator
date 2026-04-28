# Admin

React admin scaffold lives here.

## Commands

```powershell
cd .\admin
npm install
npm test
npm run test:e2e
npm run build
```

Use this folder as the future home for:

- admin panel
- operator tools
- tenant and application management UI

## E2E regression

`npm run test:e2e` starts the local `Vite` UI automatically and runs a scripted browser regression over mocked `/api/v1/admin/*` contracts.

The suite verifies:

- login and logout through the admin contour
- `start -> confirm -> reload -> load current`
- safe `replace -> confirm`
- `revoke`
- one-time artifact visibility and no `localStorage` persistence
- integration client list/create with one-time secret display
- integration client lifecycle actions: rotate secret, update scopes, deactivate and reactivate
- QR device onboarding list/create/revoke with one-time activation payload display
- user device support revoke flow
- delivery visibility and webhook subscription management

Optional environment variable:

- `ADMIN_E2E_BASE_URL`

## Integration client operator flow

The `Integration clients` workspace is the operator-ready onboarding path for external systems.

Required admin permissions:

- `integration-clients.read` for list/detail
- `integration-clients.write` for create, rotate, scope update, deactivate and reactivate

Runtime behavior:

- create and rotate show the generated `clientSecret` exactly once in the current UI state
- discard, reload, selection changes and follow-up secret-bearing commands clear the visible secret
- scope editing is checkbox-only from the supported whitelist
- lifecycle commands are bound to the selected client's `tenantId + clientId`
- state-changing calls use the shared admin API client and fetch a fresh `CSRF` token

Secrets must not be copied into issue trackers, screenshots, fixture snapshots, browser storage or audit payloads.

## QR device onboarding operator flow

The `QR device onboarding` workspace is the operator-ready path for issuing one-time Android activation artifacts.

Required admin permissions:

- `devices.read` for list/detail
- `devices.write` for create and revoke

Runtime behavior:

- create returns an opaque activation payload exactly once and renders it as an accessible `QRCodeSVG`
- discard, reload, selection changes, list refresh and revoke clear the visible activation payload
- list/detail responses show only sanitized artifact metadata and never show activation payload or code hash
- revoke is available only for `pending` artifacts and requires explicit operator confirmation
- state-changing calls use the shared admin API client and fetch a fresh `CSRF` token
- the checked-in Playwright flow covers create, one-time QR display, discard, revoke and browser storage non-persistence

Activation payloads must not be copied into issue trackers, screenshots, fixture snapshots, browser storage or audit payloads.
