# AGENTS.md

## Всегда общайся на русском
## Правила разработки
* Любая задача полностью покрыта unittest, ВСЕГДА.
* !Всегда проводить аудит по безопастности написанного кода. Любые замечания исправлять. При необходимости спрашивать.
* Исправления внешнего вида обязательно проверяй через Playwright.
* Для задач по `Android`, проблем `Gradle/Android Studio`, IDE sync и runtime-debugging обязательно использовать `Android Studio MCP`, если он доступен; не ограничиваться чтением кода и догадками.
* Если есть сомнение или неоднозначность, сначала уточни.
* НЕЛЬЗЯ создавать большие файлы (например, React-компонент >300 строк)
* НЕЛЬЗЯ использовать один общий CSS-файл
* Логика, UI и стили должны быть разделены
* Каждый модуль должен иметь одну ответственность (Single Responsibility)
* Всегда придерживаться Clean Architecture

<!-- context7 -->
Use Context7 MCP to fetch current documentation whenever the user asks about a library, framework, SDK, API, CLI tool, or cloud service -- even well-known ones like React, Next.js, Prisma, Express, Tailwind, Django, or Spring Boot. This includes API syntax, configuration, version migration, library-specific debugging, setup instructions, and CLI tool usage. Use even when you think you know the answer -- your training data may not reflect recent changes. Prefer this over web search for library docs.

Do not use for: refactoring, writing scripts from scratch, debugging business logic, code review, or general programming concepts.

## Steps

1. Always start with `resolve-library-id` using the library name and the user's question, unless the user provides an exact library ID in `/org/project` format
2. Pick the best match (ID format: `/org/project`) by: exact name match, description relevance, code snippet count, source reputation (High/Medium preferred), and benchmark score (higher is better). If results don't look right, try alternate names or queries (e.g., "next.js" not "nextjs", or rephrase the question). Use version-specific IDs when the user mentions a version
3. `query-docs` with the selected library ID and the user's full question (not single words)
4. Answer using the fetched docs
<!-- context7 -->

## Vault-First Workflow

This repository uses `OTP/` as the working knowledge vault.

### Source of truth

- `OTP/` is the source of truth for product decisions, architecture decisions, constraints, roadmap, and integration contracts.
- Source code is the source of truth for implemented behavior.
- If vault and code diverge, call it out explicitly and update the vault as part of the task when appropriate.

### Required read order for a new task

Before broad code exploration, read these files first:

1. `OTP/00 - Start Here.md`
2. `OTP/01 - Current State.md`
3. `OTP/02 - Decision Index.md`
4. `OTP/Agent/Implementation Map.md`

Then read only the domain note relevant to the task, for example under `OTP/2FA/`, `OTP/Integrations/`, `OTP/Data/`, or `OTP/Delivery/`.

### Required write-back

When a new decision is accepted or a material change is made:

- update the relevant note in `OTP/`
- add or update an `ADR` under `OTP/Decisions/` if the change is architectural or long-lived
- update `OTP/01 - Current State.md` if implementation status changed
- append a short entry to the latest session note under `OTP/Sessions/`

### Documentation style

- Prefer concise operational notes over long essays.
- Keep decisions as `ADR` records with `Status`, `Context`, `Decision`, and `Consequences`.
- Keep implementation notes pointed to actual repo paths when code appears.
- Do not create parallel truth in random markdown files outside `OTP/` unless the user asks for product-facing docs.
