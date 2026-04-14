# Decision Index

## Accepted

- `ADR-001`: строим не OTP-утилиту, а платформу `2FA/MFA` с несколькими факторами
- `ADR-002`: первый релиз делаем как отдельный `2FA Server` с `REST-first` интеграцией
- `ADR-003`: стартовая реализация идет как `modular monolith`
- `ADR-004`: мобильное приложение поддерживает и `TOTP`, и `push approval`
- `ADR-005`: knowledge vault в `OTP/` используется как рабочая точка истины для решений и контекста
- `ADR-006`: интеграционные клиенты используют `OAuth 2.0 client credentials`, мобильные устройства используют отдельные device tokens
- `ADR-007`: утвержден `MVP` technology baseline на `ASP.NET Core + PostgreSQL + Redis + Worker + React/Vite + Kotlin Android`
- `ADR-008`: мобильный фактор идет `Android-first`, `iPhone` вынесен в поздний этап
- `ADR-009`: зафиксированы bootstrap-параметры Android-проекта
- `ADR-010`: каноническая структура репозитория больше не использует верхний `src/`
- `ADR-011`: `MVP` идет как single-tenant deployment, но доменная модель сразу остается tenant-aware
- `ADR-012`: enrollment в `MVP` запускается администратором или доверенной интеграцией, self-service откладывается
- `ADR-013`: trust lifecycle устройства обязателен в `MVP`, но mandatory attestation не блокирует первую версию
- `ADR-014`: первый релиз остается `2FA Server`, но стратегическая цель продукта - будущий `IdP`
- `ADR-015`: `push approval` опционален для `on-prem` и future air-gapped профилей
- `ADR-016`: `Policy` обязателен уже в `MVP`, но как внутренний модуль монолита, а не внешний engine
- `ADR-017`: перед введением нового `enum` нужно проверять, не выражает ли он поведение, которое лучше оформить отдельными типами или стратегиями
- `ADR-018`: backend не использует `Entity Framework`; persistence строится на `Dapper` и бесплатном mapper
- `ADR-019`: миграции `PostgreSQL` управляются через `FluentMigrator` и отдельный migration runner

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
10. [[Decisions/ADR-011 - MVP Tenancy Model]]
11. [[Decisions/ADR-012 - Admin-Led Enrollment for MVP]]
12. [[Decisions/ADR-013 - Device Trust Lifecycle Without Mandatory Attestation in MVP]]
13. [[Decisions/ADR-014 - 2FA Server as MVP and IdP as Strategic Target]]
14. [[Decisions/ADR-015 - Push Optional for On-Prem and Future Air-Gapped Profiles]]
15. [[Decisions/ADR-016 - Policy Module Inside Modular Monolith]]
16. [[Decisions/ADR-017 - Prefer Types with Behavior over Enums for Extensible Domain Concepts]]
17. [[Decisions/ADR-018 - No Entity Framework, Use Dapper and Free Mapper]]
18. [[Decisions/ADR-019 - FluentMigrator for PostgreSQL Schema Management]]

Если задача относится к процессу и поддержанию контекста:

1. [[Decisions/ADR-005 - Vault First Project Memory]]
2. [[Agent/Implementation Map]]
3. [[01 - Current State]]
