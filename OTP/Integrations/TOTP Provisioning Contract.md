# TOTP Provisioning Contract

## Status

Accepted working guideline

## Цель

Зафиксировать единый provisioning contract для `TOTP-first` контуров, чтобы `backend`, `Admin UI` и `Android` не держали разные предположения о `secretUri` и связанных артефактах.

## Канонические источники

- машиночитаемый контракт: [[openapi-v1.yaml]]
- integration start flow: `backend/OtpAuth.Application/Enrollments/StartTotpEnrollmentHandler.cs`
- integration replace flow: `backend/OtpAuth.Application/Enrollments/ReplaceTotpEnrollmentHandler.cs`
- admin start flow: `backend/OtpAuth.Application/Administration/AdminStartTotpEnrollmentHandler.cs`
- admin replace flow: `backend/OtpAuth.Application/Administration/AdminReplaceTotpEnrollmentHandler.cs`
- mobile consumer: `mobile/totp-domain/src/main/kotlin/ru/dt1520/security/authenticator/totp/domain/OtpAuthUriParser.kt`

## Текущий shape provisioning response

- `enrollmentId`
- `status`
- `hasPendingReplacement`
- `secretUri`
- `qrCodePayload`

`secretUri` и `qrCodePayload` не входят в обязательный shape общего enrollment read model и считаются optional artifact fields.

## Visibility rules

- provisioning artifact разрешено возвращать только в `start` и `replace`
- `read`, `confirm` и `revoke` не должны повторно возвращать provisioning artifacts
- admin current enrollment read model по `tenantId + externalUserId` не должен раскрывать provisioning artifacts
- UI и mobile consumers не должны ожидать artifact вне `start`/`replace` response

## Artifact semantics

- `secretUri` содержит canonical `otpauth://totp/...` provisioning URI
- `qrCodePayload` сейчас равен `secretUri` и не является отдельным PNG/blob контрактом
- текущий backend формирует URI с `issuer`, `label`, `secret`, `algorithm`, `digits` и `period`
- текущий стабильный runtime profile для `MVP`: `SHA1`, `6` digits, `30` seconds
- если `issuer` не передан явно, backend использует default `OTPAuth`

## Stability rules

- изменение `algorithm`, `digits`, `period` или serialization shape provisioning URI считается contract change
- любой такой change должен синхронно обновлять `OpenAPI`, `vault`, backend tests и mobile parser/tests
- если появится отдельный QR image payload, его нужно фиксировать как новый explicit field, а не неявно менять семантику `qrCodePayload`

## Security rules

- provisioning artifact показывается как one-time material и должен discard-иться после ухода в read flow, reload или завершения session-specific UX
- provisioning artifact не должен храниться в постоянном клиентском кэше, логах, audit payloads или user-facing read models
- mobile app может принимать artifact локально, но после save должен держать только secure-store snapshot, а не secret-bearing preview

## Consumer notes

### Admin UI

- может показывать artifact только внутри `start/replace` session
- не должен пытаться восстановить artifact через read model

### Android TOTP-first

- импортирует `secretUri` как canonical provisioning artifact
- manual fallback остается UX-резервом, но не меняет серверный контракт

## Связанные заметки

- [[../Delivery/Admin UI MVP Plan]]
- [[../Delivery/Admin and Android Readiness Gates]]
- [[../Product/Android TOTP-First Plan]]
- [[../Security/Security Model MVP]]
