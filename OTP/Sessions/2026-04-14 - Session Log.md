# Session Log 2026-04-14

## Entries

- Создан `vault-first` протокол работы с проектом.
- Добавлены верхнеуровневые заметки `Start Here`, `Current State`, `Decision Index`, `Open Questions`, `Implementation Map`.
- Зафиксировано, что `OTP/` является рабочей точкой истины для решений и проектного контекста.
- Ранее созданные заметки по `2FA/MFA` оставлены в `OTP/2FA/` и включены в общий маршрут чтения.
- `OTP/Architecture/` подготовлена как целевая зона нормализации, но перенос заметок туда отложен из-за файловой блокировки.
- Созданы канонические доменные ветки `Architecture`, `Data`, `Integrations`, `Product`, `Delivery`.
- Добавлены `ERD`, черновой `OpenAPI v1` и план реализации на `8-12` недель.
- `OTP/2FA/` переведена в режим исторического слепка, а не основной точки чтения.
- `openapi-v1.yaml` усилен: добавлены `securitySchemes`, единая ошибка `Problem`, top-level `webhooks`, `Idempotency-Key` и более строгие схемы ресурсов.
- Добавлены `OAuth 2.0` token flow для интеграционных клиентов, refresh flow для device tokens, operation-level callbacks, response headers и отдельный `ADR-006`.
- `openapi-v1.yaml` прогнан через `Redocly CLI`; после исправлений callback/webhook security, operationId, license и headers спецификация валидна без предупреждений.
- Утвержден baseline: `ASP.NET Core`, `PostgreSQL`, `Redis`, `Outbox + Worker`, `React + Vite`, `Kotlin Android`, observability stack на `OpenTelemetry + Prometheus + Grafana + Loki + Tempo`.
- `iPhone` выведен из обязательного `MVP` и оставлен как growth track.
- Добавлена deployment-схема `MVP`.
- Зафиксированы bootstrap-параметры Android-проекта: `DT 1520 Authenticator`, `ru.dt1520.security.authenticator`, `mobile`, `Min SDK 28`, `Empty Activity`, `Compose`, `Kotlin DSL`.
- Созданы scaffold-проекты: `mobile`, `backend`, `admin`, `infra`.
- Для `admin` создан `React + Vite` scaffold, установлен `npm install`, build проходит.
- Для `backend` создан `ASP.NET Core` scaffold с solution и модулями; restore/build успешно выполнены после запуска вне sandbox.
- Добавлены локальные примеры `MCP`-конфигов в `config/mcp`.
- Принято решение отказаться от верхнего `src/`; канонические корни переключены на `backend`, `mobile`, `admin`.
- Выполнен cleanup: удалены старый `src/`, внутренний `backend/src/`, generated-cache директории и локальные IDE/build хвосты.
- После переноса workspace повторно проверены зависимости: `admin` снова проходит `npm install` и `npm run build`, `backend` проходит `dotnet restore`, `mobile` резолвит Gradle-зависимости при корректном `JAVA_HOME`.
- Для `backend` добавлен `Directory.Solution.props` с отключением параллельной solution-сборки, чтобы убрать нестабильные file locks в `artifacts/obj`.
- Обновлены `README` в корне и в рабочих корнях `admin/backend/mobile` с актуальными командами для этой папки.
- После проверки удалены локальные verification/build артефакты из `admin`, `backend` и `mobile`, чтобы не оставлять generated хвосты в workspace.
