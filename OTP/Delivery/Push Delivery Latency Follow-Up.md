# Push Delivery Latency Follow-Up

## Status

Required follow-up after first successful ProjectManager manual pilot; primary diagnostic contour moved to future reference `Desktop + Backend` stand

## Context

Первый ручной `ProjectManager` pilot прошел успешно: пользователь вошел через existing `Keycloak` flow, protected `VCS` operation создала `push` challenge, Android emulator получил approval prompt, пользователь подтвердил его, а основной step-up contour сработал.

Наблюдаемый residual:

- между запросом в `ProjectManager` и появлением challenge в Android emulator проходит около `~60s`

Это не считается дефектом security-flow: approve/deny остается device-bound и fail-closed. Но это считается UX/operational blocker перед нормальным pilot usage, потому что пользователь ожидает near-real-time появление запроса.

Offline verification не проводилась. Все текущие выводы относятся к online/live contour; поведение при потере сети, недоступности `DT-1520` runtime или offline fallback должно быть оформлено отдельным сценарием, если это войдет в pilot scope.

После обсуждения решено не использовать `ProjectManager` как основной стенд для latency hardening. `ProjectManager` остается enterprise pilot proof, но в нем слишком много дополнительных компонентов для чистой диагностики delivery/polling path. Основной повторный цикл должен идти через [[Reference Desktop Backend Stand]].

Под offline-code fallback в ближайшем плане понимается не offline backend-login. `Android` генерирует `TOTP` локально без сети, но reference backend и `DT-1520` runtime остаются online, чтобы сохранить centralized verification, replay defense, audit и rate limiting.

## Current Hypothesis

Основная гипотеза: задержка находится в delivery/polling contour, а не в `CreateChallenge` или `approve` path.

На текущем pilot runtime:

- `PushDelivery:Provider=logging`
- real `FCM` push provider не подключен
- Android получает pending challenges через device runtime inbox/polling path
- значит near-real-time delivery зависит от foreground polling cadence и worker/outbox timings

## Required Investigation Order

1. Замерить timestamps для одного нового challenge в reference `Desktop + Backend` stand:
   - время submit в desktop shell
   - время получения request в reference backend
   - время создания `auth.challenges`
   - время постановки `auth.push_challenge_deliveries`
   - время worker processing delivery row
   - время появления challenge в `GET /api/v1/devices/me/challenges/pending`
2. Проверить текущий Android foreground polling interval и lifecycle behavior:
   - как часто app вызывает pending inbox
   - делает ли immediate refresh при возврате в foreground
   - нет ли лишнего backoff после пустого inbox
3. Проверить worker delivery profile:
   - частота цикла `push_challenge_delivery`
   - нет ли очереди/retry задержек
   - не добавляет ли `logging` provider artificial timing gap
4. Принять pilot decision:
   - временно уменьшить foreground polling interval для pilot
   - добавить immediate refresh после открытия `Push Approvals`
   - или подключать real push provider для near-real-time delivery

## Success Criteria

Минимальный приемлемый pilot UX:

- challenge появляется в Android UI не дольше чем за `5-10s` при открытом приложении
- при закрытом/фоновом приложении поведение явно описано как polling-only limitation или закрыто real push provider-ом
- latency source подтвержден измерениями, а не догадкой
- security boundary не ослаблена: нет fail-open apply, нет bypass device binding, нет хранения raw secrets/log payloads

## Non-Goals

- не менять primary auth `ProjectManager/Keycloak`
- не делать login MFA в рамках этого follow-up
- не считать этот follow-up offline-readiness проверкой
- не добавлять новый mobile onboarding UX
- не подключать `iOS`
- не делать полноценный notification product surface до решения по provider

## Related Notes

- [[ProjectManager Pilot Integration Story]]
- [[Reference Desktop Backend Stand]]
- [[Official Dotnet Integration SDK]]
- [[../Product/Android Push Runtime Plan]]
- [[MVP Closure Iteration Plan]]
- [[../Decisions/ADR-031 - Push Challenges Are Bound to a Single Active Device]]
- [[../Decisions/ADR-032 - Push Delivery Uses Configurable Provider Adapter]]
- [[../Decisions/ADR-034 - Pilot Integrations Keep Existing Primary Auth and Use Step-Up MFA]]
