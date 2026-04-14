# Workspace Bootstrap

## Созданные балванки

### Mobile

- `mobile`
- Android Studio project
- `Kotlin`
- `Jetpack Compose`
- `Min SDK 28`

### Backend

- `backend/OtpAuth.sln`
- `backend/OtpAuth.slnx`
- `backend/OtpAuth.Api`
- `backend/OtpAuth.Worker`
- `backend/OtpAuth.Application`
- `backend/OtpAuth.Domain`
- `backend/OtpAuth.Infrastructure`

### Admin

- `admin`
- `React + Vite` scaffold files
- `npm install` выполнен
- production build проходит

### Infra

- `infra`

### MCP

- `config/mcp/README.md`
- `config/mcp/android-studio.mcp.json.example`
- `config/mcp/codex.mcp.json.example`

## Текущий технический статус

- mobile scaffold существует и совпадает с утвержденными параметрами
- admin scaffold существует и собирается
- backend scaffold существует, `dotnet restore` и `dotnet build` проходят
- в текущем окружении для backend restore/build потребовался запуск вне sandbox
- после переноса workspace подтверждено: `admin` заново проходит `npm install` и `npm run build`
- после переноса workspace подтверждено: `mobile` резолвит Gradle-зависимости при корректном `JAVA_HOME`
- для `backend` solution build закреплен без параллельных workers, чтобы избежать file locking в `artifacts/obj`
- каноническая структура репозитория больше не использует верхний `src/`
- legacy-дубли и локальные generated/cache директории уже очищены

## Практический вывод

Для продолжения работы сейчас достаточно:

- Android Studio для `mobile`
- Node.js для `admin`
- .NET SDK для `backend`
- настроенный `JAVA_HOME` в shell или IDE для запуска Gradle-команд из терминала
