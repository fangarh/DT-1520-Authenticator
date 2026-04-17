# Android App Bootstrap

## Утвержденные параметры проекта

- `App name`: `DT 1520 Authenticator`
- `Package / namespace`: `ru.dt1520.security.authenticator`
- `Location`: `D:\Projects\2026\DT-1520-Authenticator\mobile`
- `Language`: `Kotlin`
- `Template`: `Empty Activity`
- `UI`: `Jetpack Compose`
- `Min SDK`: `28`
- `Build configuration`: `Kotlin DSL`

## Смысл этого выбора

- приложение идет как нативный `Android`-клиент
- baseline современный, без лишней legacy-нагрузки
- namespace сразу задан в стабильном корпоративном виде

## Практическое правило

После создания проекта именно эти параметры считаются каноническими, если отдельно не принято новое решение.

## Workspace note

После переноса workspace канонический путь обновлен на текущий monorepo root. Namespace, стек и bootstrap-параметры при этом не менялись.
