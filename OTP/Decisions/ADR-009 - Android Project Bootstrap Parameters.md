# ADR-009: Android Project Bootstrap Parameters

## Status

Accepted

## Context

Для запуска mobile-трека нужен зафиксированный набор параметров проекта, чтобы не переименовывать приложение и namespace позже.

## Decision

Для первого `Android`-клиента принимаются такие bootstrap-параметры:

- `App name`: `DT 1520 Authenticator`
- `Package`: `ru.dt1520.security.authenticator`
- `Location`: `D:\Projects\2026\OtpAuth\mobile`
- `Language`: `Kotlin`
- `Template`: `Empty Activity`
- `UI`: `Jetpack Compose`
- `Min SDK`: `28`
- `Build config`: `Kotlin DSL`

## Consequences

- старт проекта в Android Studio стандартизирован
- namespace не придется стабилизировать постфактум
- mobile track можно запускать без дополнительного обсуждения bootstrap-настроек
