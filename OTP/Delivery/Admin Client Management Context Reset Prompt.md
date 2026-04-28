# Admin Client Management Context Reset Prompt

## Status

Ready to use

## Purpose

Этот prompt нужен для Codex после очистки контекста между итерациями [[Admin Client Management Iteration Plan]].

## Prompt

```text
Ты работаешь в репозитории `D:\Projects\2026\DT-1520-Authenticator`.

Всегда отвечай на русском.

Это continuation после очистки контекста. Не начинай с нуля: сначала восстанови рабочий контекст из vault.

Обязательный reading path:

1. `AGENTS.md`
2. `OTP/00 - Start Here.md`
3. `OTP/01 - Current State.md`
4. `OTP/02 - Decision Index.md`
5. `OTP/Agent/Implementation Map.md`
6. `OTP/Delivery/MVP Closure Iteration Plan.md`
7. `OTP/Delivery/Admin Client Management Follow-Up.md`
8. `OTP/Delivery/Admin Client Management Iteration Plan.md`
9. latest session note under `OTP/Sessions/`

Текущий productization order:

1. Admin Client Management
2. QR Device Onboarding
3. Official .NET SDK under `lib/`
4. Reference Desktop + Backend stand under `rdb_stand/`
5. latency and online TOTP fallback hardening

Твоя задача: продолжить Admin Client Management с первой незакрытой итерации из `OTP/Delivery/Admin Client Management Iteration Plan.md`.

Перед изменениями:

- проверь `git status --short`
- не откатывай чужие изменения
- найди фактические entry points через `rg`
- если итерация касается библиотек/framework/API syntax, используй Context7 по правилам `AGENTS.md`

Незыблемые security boundaries:

- plaintext `client_secret` показывается только один раз при create/rotate
- `client_secret` и `client_secret_hash` не попадают в read models, logs, audit events, browser persistence or snapshots
- state-changing admin endpoints require admin cookie session + `CSRF`
- admin permissions are separate from integration client scopes
- lifecycle changes must preserve token invalidation via `last_auth_state_changed_utc`
- desktop apps must not receive or store integration `client_secret`

Engineering rules:

- одна сессия = одна итерация из плана
- все изменения покрывать unit/integration/browser tests according to touched layer
- для UI обязательно `npm test`, `npm run build`, `npm run test:e2e`; при визуальных изменениях использовать Playwright verification
- для backend использовать existing verification scripts/tests; если sandbox blocks `backend/artifacts`, явно указать и повторить по правилам окружения
- всегда проводить security review написанного кода и исправлять замечания
- документация входит в Definition of Done

После реализации итерации обновить:

- `OTP/Delivery/Admin Client Management Iteration Plan.md` status for completed iteration
- `OTP/Delivery/Admin Client Management Follow-Up.md`, если изменился общий статус
- `OTP/01 - Current State.md`
- `OTP/Agent/Implementation Map.md`
- relevant docs under `docs/`, if user/operator/integration behavior changed
- latest session note under `OTP/Sessions/`

В финальном ответе:

- кратко перечисли, какая итерация закрыта
- укажи измененные ключевые файлы
- укажи выполненные проверки
- отдельно укажи security review result
- если что-то не удалось проверить, объясни почему
```

## Notes

- Если пользователь явно задаст номер итерации, выполнять его.
- Если пользователь не задаст номер, брать первую незакрытую итерацию из плана.
- Если фактический код уже частично реализует итерацию, сначала зафиксировать gap analysis и сузить scope.

