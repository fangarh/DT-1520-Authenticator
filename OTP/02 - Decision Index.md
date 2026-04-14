# Decision Index

## Accepted

- `ADR-001`: строим не OTP-утилиту, а платформу `2FA/MFA` с несколькими факторами
- `ADR-002`: первый релиз делаем как отдельный `2FA Server` с `REST-first` интеграцией
- `ADR-003`: стартовая реализация идет как `modular monolith`
- `ADR-004`: мобильное приложение поддерживает и `TOTP`, и `push approval`
- `ADR-005`: knowledge vault в `OTP/` используется как рабочая точка истины для решений и контекста
- `ADR-006`: интеграционные клиенты используют `OAuth 2.0 client credentials`, мобильные устройства используют отдельные device tokens
- `ADR-007`: утвержден `MVP` technology baseline на `ASP.NET Core + PostgreSQL + Redis + Worker + React/Vite + Kotlin Android`
- `ADR-008`: мобильный фактор идет `Android-first`, `iPhone` остается точкой роста
- `ADR-009`: зафиксированы bootstrap-параметры Android-проекта
- `ADR-010`: каноническая структура репозитория больше не использует верхний `src/`

## Reading Path

Если задача относится к продукту и архитектуре:

1. [[Decisions/ADR-001 - MFA Platform Instead of OTP Utility]]
2. [[Decisions/ADR-002 - REST-First 2FA Server]]
3. [[Decisions/ADR-003 - Modular Monolith for MVP]]
4. [[Decisions/ADR-004 - Mobile App as TOTP and Push Factor]]
5. [[Decisions/ADR-006 - OAuth2 for Integrations and Device Tokens for Mobile]]
6. [[Decisions/ADR-007 - MVP Technology Baseline]]
7. [[Decisions/ADR-008 - Android First Mobile Factor]]
8. [[Decisions/ADR-009 - Android Project Bootstrap Parameters]]
9. [[Decisions/ADR-010 - Monorepo Root Layout Without Top-Level Src]]

Если задача относится к процессу и поддержанию контекста:

1. [[Decisions/ADR-005 - Vault First Project Memory]]
2. [[Agent/Implementation Map]]
3. [[01 - Current State]]
