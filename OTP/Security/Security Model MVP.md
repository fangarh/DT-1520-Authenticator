# Security Model MVP

## Status

Draft

## Цель

Зафиксировать минимально достаточную security-модель `MVP`, чтобы backend, mobile и admin реализовывались без опасных временных упрощений.

## Защищаемые активы

- `TOTP` secrets
- `backup codes`
- bearer tokens интеграционных клиентов
- device access и refresh tokens
- enrollment и activation artifacts
- `challenge` state и итог подтверждения
- audit trail
- конфигурация интеграционных клиентов

## Границы доверия

### Внешний интеграционный клиент

- доверяется только после успешного `OAuth 2.0 client credentials`
- имеет scope-ограниченный доступ
- не получает доступ к внутренним секретам факторов

### Mobile device

- устройство не считается trusted по умолчанию
- доверие появляется только после activation flow
- lifecycle токенов и статус устройства контролируются через `Device Registry`

### Admin/operator

- имеет повышенный риск
- все административные действия должны аудироваться
- чувствительные операции требуют более строгой авторизации, чем обычные read-only действия

## Обязательные security controls

### Secrets at rest

- `TOTP` secret хранится только в зашифрованном виде
- `backup codes` хранятся только как hash
- `client_secret` интеграционного клиента хранится только как hash
- версии ключей шифрования фиксируются в данных
- ключи шифрования не хранятся в репозитории или app config

### Key management

- `MVP` должен поддерживать внешний `Vault` или `KMS` как целевую модель
- для локальной разработки допускается dev-only substitute, но не как production pattern
- ротация ключей должна быть предусмотрена через `key version`, а не через overwrite
- для `TOTP` decrypt-path должен уметь читать current и legacy keys одновременно
- для integration access token validation должен поддерживаться signing key ring, а новые токены должны выпускаться с `kid`

### Tokens

- интеграционные клиенты используют только short-lived bearer token
- device access token и refresh token живут отдельно от integration auth model
- refresh token устройства должен быть revocable
- токены не логируются и не возвращаются повторно в telemetry
- integration access token должен поддерживать revocation и introspection до перехода к полноценному auth subsystem

### Transport security

- все production-вызовы только через `HTTPS`
- callback и webhook endpoints требуют подпись или иной механизм верификации источника
- `push` delivery не должна раскрывать чувствительные payload без server-side проверки
- отсутствие внешних `push` providers не должно ломать базовый authentication flow

### Input and protocol safety

- все входные данные валидируются на boundary layer
- state-changing endpoints поддерживают `Idempotency-Key`, где операция не должна дублироваться
- correlation id отделяется от security token и не используется как credential
- ошибки наружу не раскрывают внутренние детали модели, хранилища или ключей
- resource access для `Challenges` ограничивается не только по bearer token, но и по `tenant/application client` scope
- revoked bearer token должен отбрасываться на auth boundary до входа в handler

### Replay and brute-force protection

- `TOTP` verify защищается rate limit и anti-replay правилами
- device activation защищается ограничением количества попыток и сроком жизни activation artifact
- refresh token flow имеет защиту от replay и возможность принудительного revoke
- polling и status endpoints ограничиваются по клиенту и tenant
- повторное использование уже принятого `TOTP` time step запрещается persistent reservation-слоем
- при превышении лимита verify-flow возвращает `429` и `Retry-After`, не раскрывая внутренние счетчики

### Audit

- аудируются: token issuance, token refresh, enrollment start/confirm, device activation, approve, deny, revoke, admin policy change
- audit payload редактирует или исключает секреты, коды, токены и `PII`, если она не нужна для расследования
- audit trail должен быть append-oriented на уровне процесса

## Решения, которые нельзя откладывать в коде

- нельзя хранить секреты в открытом виде
- нельзя класть токены в логи
- нельзя делать verify endpoints без rate limiting
- нельзя оставлять device revoke как soft UI-only действие без фактической серверной блокировки
- нельзя смешивать integration token model и device token model в один credential type

## Текущее bootstrap-ограничение backend

- текущий integration auth layer для `Challenges` уже выпускает JWT через bootstrap `/oauth2/token` и читает integration clients из `PostgreSQL`
- `Challenge` persistence больше не process-local: state хранится в `PostgreSQL`
- `VerifyTotp` теперь использует enrollment-backed secret из `PostgreSQL`
- `TOTP` secret шифруется на запись и расшифровывается только внутри verifier path
- verify-attempts пишутся append-only в `auth.challenge_attempts`
- anti-replay reservations пишутся в `auth.totp_used_time_steps`
- revoked integration access tokens пишутся в `auth.revoked_integration_access_tokens`
- bootstrap client seed и bootstrap `TOTP` enrollment seed вынесены в явные операции migration runner-а и не выполняются автоматически на старте `Api`
- если signing key не задан конфигурацией, bootstrap OAuth использует process-local ephemeral signing key; это допустимо только для локального dev/bootstrap режима
- `TOTP` protection key пока задается через env-managed `TotpProtection__CurrentKey`; это допустимо только как bootstrap stage до интеграции с `Vault/KMS`
- `TOTP` protector уже поддерживает current + legacy keys по `key version`
- bootstrap JWT issuer уже поддерживает current + legacy signing keys и validation по `kid`
- tenant/application scoping уже проводится в `Application` и persistence слое
- bootstrap host явно использует console logging provider и не зависит от Windows `EventLog`, чтобы security-flow не падал из-за инфраструктурных прав
- token revocation и introspection уже реализованы в bootstrap-слое
- automated re-encryption для `TOTP` secrets уже реализован как отдельная maintenance-операция migration runner-а
- cleanup/retention для `challenge_attempts`, `totp_used_time_steps` и `revoked_integration_access_tokens` уже реализован как отдельная maintenance-операция migration runner-а
- полноценный signing key rotation workflow и client lifecycle management все еще не реализованы

## MVP security gates для backend

- все новые endpoint-ы имеют явную auth модель
- все команды в `Application` слое валидируют вход
- все persistence операции используют безопасные параметризованные запросы или ORM mapping
- все security-critical сценарии покрыты unit и integration tests
- все response contract-ы проверены на отсутствие утечки секретов

## MVP security gates для mobile

- токены и локальные секреты хранятся в `Keystore`-совместимом механизме
- approve action защищен локальной user presence проверкой, если это не конфликтует с offline fallback
- логи клиента не содержат activation code, токены и секреты
- backup/export механики не должны выносить секреты по умолчанию

## MVP security gates для admin

- админские операции должны иметь явную модель ролей и прав
- UI не должен показывать чувствительные значения повторно после создания
- destructive actions требуют подтверждения и аудита

## Решения, уже зафиксированные в ADR

- `MVP` идет как `single-tenant deployment`, но доменная модель остается tenant-aware
- enrollment первой версии идет через admin-led или trusted integration flow
- trust lifecycle устройства обязателен, но mandatory attestation не блокирует `v1`
- стратегическая цель продукта - будущий `IdP`, но `MVP` остается отдельным `2FA Server`
- `push approval` не является обязательной опорой для `on-prem` и future air-gapped профилей
- `Policy` обязателен уже в `MVP` как внутренний модуль с `deny by default`

## Минимальный security backlog перед первым vertical slice

1. Зафиксировать storage policy для `backup codes`, refresh tokens и будущих device tokens.
2. Описать security profile для future air-gapped enterprise режима.
3. Реализовать operational rotation workflow для signing keys, включая rollout и retirement legacy keys.
4. Добавить lifecycle management для integration clients: secret rotation, deactivate/reactivate, audit.
