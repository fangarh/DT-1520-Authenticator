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
- tenant directory list/detail, quick create and manual create
- selected tenant management tabs for API clients, users/devices, runtime policy and reports
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

## Tenant directory operator flow

The `Tenant directory` workspace is the primary setup entry for tenant-centric onboarding.

Required admin permissions:

- `tenants.read` for list/detail
- `tenants.write` for quick create and manual create

Runtime behavior:

- quick create generates tenant, application and initial API client IDs server-side
- quick create returns the initial `clientSecret` exactly once in the current UI state
- manual create supports advanced migration/demo tenants without returning any secret
- discard, reload and follow-up commands clear one-time client secrets from browser state
- state-changing calls use the shared admin API client and fetch a fresh `CSRF` token

Tenant directory read models must not show `client_secret`, `client_secret_hash` or activation payloads.

## Tenant management operator flow

The `Tenant management` workspace opens from a selected tenant directory record and keeps daily actions under that tenant context.

Runtime behavior:

- API client create, rotate, scope update, deactivate and reactivate use the selected tenant/client context
- selected-user device lookup, device revoke and QR issue use the selected tenant/application/user context
- runtime configuration shows callback policy metadata only, without callback URLs or secrets
- reports summarize existing delivery/device read models and do not include raw callback payloads, push tokens or QR activation payloads
- reports now include a selected-tenant snapshot over recent deliveries, callback/webhook health, selected-user device counts, recent QR artifact statuses and last approval/device activity markers
- one-time client secrets and QR activation payloads remain current-session only and are cleared by discard/follow-up secret-bearing commands

This workspace is the primary operator path after tenant quick create; the older copy-paste workspaces are fallback-only for admin sessions that do not have tenant permissions.

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
