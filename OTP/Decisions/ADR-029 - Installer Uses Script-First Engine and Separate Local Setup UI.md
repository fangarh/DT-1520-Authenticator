# ADR-029: Installer Uses Script-First Engine and Separate Local Setup UI

## Status

Accepted

## Context

К `2026-04-15` в репозитории уже есть первый рабочий installer contour:

- `infra/scripts/install.ps1`
- `infra/scripts/Installer.Common.ps1`
- `Docker Compose`-based packaging и bootstrap profile
- baseline preflight, `install/update/recover` modes и sanitized status report

Одновременно возник практический вопрос, нужен ли installer UI.

Полный отказ от UI создает лишнее трение для `on-prem` операторов. Но перенос installer flow в текущий runtime `admin/` или создание отдельной orchestration-логики внутри UI ломает уже принятые ограничения:

- `ADR-025`: setup plane отделен от runtime `Admin UI`
- installer не должен становиться internet-facing runtime surface
- secrets не должны жить в browser storage, runtime API payload-ах или дублироваться в нескольких orchestration слоях
- `install/update/recover` path должен оставаться автоматизируемым и testable без браузера

Значит вопрос не в выборе `UI или scripts`, а в правильной роли каждого слоя.

## Decision

- каноническим installer engine остается script-first orchestration в `infra/scripts/install.ps1` и `infra/scripts/Installer.Common.ps1`
- install/update/recover semantics, preflight checks, bootstrap sequencing и security guardrails определяются engine-контрактом, а не UI
- installer UI допускается как следующий слой, но только как отдельный локальный setup surface поверх того же engine-контракта
- installer UI не размещается в текущем runtime `admin/`
- installer UI не должен требовать отдельной business-логики orchestration, которая расходится со script engine
- installer UI остается локальным или явно ограниченным bootstrap surface, а не обычным public/runtime endpoint
- перед реализацией installer UI engine должен получить machine-readable contract:
  - structured validation errors
  - structured step results
  - sanitized status/report payload
  - формализованный install manifest или equivalent input contract

## Consequences

- `infra/scripts/*` остаются источником истины для installer behavior и проще покрываются automated tests
- команда может сначала довести installer engine и recovery path до надежного состояния, не блокируясь на UI
- отдельный локальный wizard остается возможным и желательным follow-up, но не размывает security boundary между setup plane и runtime `Admin UI`
- `admin/` не превращается в привилегированную оболочку для host-level lifecycle операций
- browser UX для installer должен проектироваться как thin shell над уже стабилизированным engine-контрактом
