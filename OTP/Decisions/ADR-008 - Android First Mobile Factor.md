# ADR-008: Android-First Mobile Factor

## Status

Accepted

## Context

Для security-sensitive приложения второй фактор должен быть нативным. При этом обязательная поддержка `iPhone` на первом этапе не требуется.

## Decision

Мобильное приложение первой версии строится как `Android`-клиент на `Kotlin`.

`iPhone` не входит в обязательный `MVP`, но остается planned growth track.

## Consequences

- первая версия мобильного фактора быстрее и проще в реализации
- безопасность и platform integration на `Android` будут сильнее, чем при cross-platform старте
- для выхода на `iPhone` позже потребуется отдельный технический трек
