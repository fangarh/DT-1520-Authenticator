# DT-1520 Authenticator Docs

Standalone React documentation page for project handoff.

## Commands

```powershell
cd .\docs
npm install
npm test
npm run build
npm run test:e2e
```

## Runtime model

- The docs app is static and has no backend dependency.
- `vite.config.ts` uses `base: "./"` so `dist/` can be served from a static host or behind a nested path.
- Documentation content lives in `src/data/documentation.ts`.
- UI, content data and styles are separated between `src/app`, `src/shared` and CSS modules.
- The Admin UI and Android sections are the current handoff surfaces for installation, permissions, integration client onboarding, secret rotation, deactivation/reactivation, QR device onboarding and troubleshooting.
- QR device onboarding is documented as the productized operator-to-Android activation path; debug-only activation helpers are not a production handoff path.
- The SDK section points to `lib/Dt1520.Authenticator.slnx`, including current Client APIs for OAuth, challenges, online TOTP, device routing, push target selection and callback signature validation.
- The ASP.NET Core SDK handoff documents DI/options/`IHttpClientFactory` helpers and raw-body callback validation while keeping secrets in backend-only configuration.
- The Desktop SDK handoff documents approval session polling against an integrator backend only; no desktop-held `client_secret`, bearer token or direct DT-1520 base URL is introduced.
- The SDK getting-started handoff points to package README files and `lib/samples/aspnetcore-protected-operation/README.md` for create challenge, callback validation, status polling and online TOTP fallback.
- The reference stand handoff points to `rdb_stand/ReferenceDesktopBackendStand.slnx`: ASP.NET Core backend, console desktop shell, sanitized `--preflight`/live-readiness checks, env-var runbook and tests for signed callbacks, callback URL hardening, status polling and online TOTP fallback.

## Security notes

- Do not add real secrets, tokens, signing material, private URLs with credentials, raw callback payloads or Android `pushToken` values to the docs app.
- Debug-only Android activation helpers may be documented only as non-production pilot tooling.
