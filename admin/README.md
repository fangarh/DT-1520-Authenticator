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

Optional environment variable:

- `ADMIN_E2E_BASE_URL`
