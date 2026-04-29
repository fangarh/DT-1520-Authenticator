# Final Integrated Verification Context Reset Prompt

Мы в `D:\Projects\2026\DT-1520-Authenticator`. Всегда отвечай по-русски.

Сначала прочитай:

1. `AGENTS.md`
2. `OTP/00 - Start Here.md`
3. `OTP/01 - Current State.md`
4. `OTP/02 - Decision Index.md`
5. `OTP/Agent/Implementation Map.md`
6. `OTP/Delivery/Reference Desktop Backend Stand.md`
7. `OTP/Delivery/QR Device Onboarding Runtime URL Follow-Up.md`
8. `OTP/Delivery/Final Integrated Verification Closure Plan.md`

Текущий фокус: закрыть `Final Integrated Verification Gate` через server-owned `ReferenceBackend` contour, не начинать новый gateway и не использовать third-party tunnels.

Текущий live contour:

- `ReferenceBackend`: `https://admin.ghostring.ru:18444/`
- `ReferenceBackend` callback: `https://admin.ghostring.ru:18444/api/reference/callbacks/dt1520`
- DT-1520/Admin public runtime: `https://admin.ghostring.ru:18443/`
- `ReferenceBackend` inside compose calls DT-1520 through `http://api:8080/`
- scope remains `challenges:read challenges:write`
- target tenant: `6c45188d-1a85-4e54-b139-11ff7d592b4e`
- target application client: `c94976c1-17fc-44d2-93d9-34fcb9afe63c`
- target integration client: `desktop-mvp`

Known live results already proven:

- public ReferenceBackend health/readiness is healthy/ready;
- device QR activation works;
- fresh debug Android activation creates `Active` device with `isPushCapable=true`;
- push approve through Desktop/WPF + ReferenceBackend works;
- push deny through Desktop/WPF + ReferenceBackend works;
- Android currently needs app restart/foreground reload to see newly created pending push;
- online `TOTP` fallback currently returns `409` when attempted against the push-selected challenge because `verify-totp` cannot verify a `Push` challenge.

Accepted plan:

1. Implement explicit `TOTP` fallback in `ReferenceBackend`: fallback creates/verifies a real `Totp` challenge for the same reference session instead of verifying against the push challenge.
2. Add Android foreground pending refresh so new pending push items appear without app restart.
3. Add combined operator QR for device activation plus `TOTP` enrollment/import.
4. Extend Android QR consume flow to activate device and import/confirm TOTP from one scan.
5. Run final live gate: combined QR, push approve, push deny, explicit online `TOTP` fallback.
6. Sync docs/vault/session notes.

Start with Iteration 1 from `OTP/Delivery/Final Integrated Verification Closure Plan.md`.

Constraints:

- Do not print secrets/access tokens/callback signing secrets/raw QR payloads/real push tokens.
- Do not use trycloudflare/ngrok or other third-party tunnels.
- Keep Desktop free of integration `client_secret`.
- Keep TOTP code transient; do not persist it in WPF settings, logs or reference session records.
- Follow tests/security review/docs/vault sync as DoD.
- For Android tasks use Android MCP; if emulator is needed and not running, start it rather than skipping live verification.
