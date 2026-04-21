# Ghostring Pre-Deploy Checklist

## Status

Accepted working note

## Goal

Дать короткий и воспроизводимый checklist перед первым pilot rollout `DT-1520 Authenticator` на сервер `ghostring`.

Этот checklist нужен для controlled pre-deploy path. Он не заменяет `Ghostring Pilot Deployment Profile` и не считается runbook-ом полного deploy/recovery.

## Reading path before execution

1. [[Ghostring Pilot Deployment Profile]]
2. [[On-Prem Delivery]]
3. [infra/README.md](D:\Projects\2026\DT-1520-Authenticator\infra\README.md)
4. [infra/docker-compose.ghostring.yml](D:\Projects\2026\DT-1520-Authenticator\infra\docker-compose.ghostring.yml)
5. [infra/env/ghostring.runtime.env.example](D:\Projects\2026\DT-1520-Authenticator\infra\env\ghostring.runtime.env.example)
6. [infra/nginx/admin.ghostring.ru.conf.example](D:\Projects\2026\DT-1520-Authenticator\infra\nginx\admin.ghostring.ru.conf.example)

## Assumptions

- сервер `ghostring` уже признан pilot-suitable для нагрузки `<=10` пользователей
- public DNS `admin.ghostring.ru` доступен именно для этого сервера
- existing `PostgreSQL` / `dt-auth` остается основным data store
- `compose-managed postgres` не используется
- host `nginx` остается public edge
- `admin` container будет доступен только через loopback `127.0.0.1:18443`

## Phase 1. Repository And Runtime Boundary

### Stop/go checks

- репозиторий склонирован в отдельный рабочий каталог
- runtime secrets не планируются внутри git-tracked путей
- выбран host-level runtime root, например `/opt/dt-1520-authenticator`

### Actions

1. Создать runtime root вне репозитория.
2. Создать подкаталоги:
   - `runtime.env`
   - `tls/`
   - `secrets/` при необходимости для `FCM`
3. Скопировать пример env:
   - `infra/env/ghostring.runtime.env.example -> /opt/dt-1520-authenticator/runtime.env`

### Expected result

- runtime boundary отделен от git checkout
- есть одно каноническое место для env, certs и optional service account files

## Phase 2. Docker Compose Availability

### Stop/go checks

- `docker` установлен и daemon запущен
- `docker compose version` работает

### Actions

1. Проверить:
   - `docker version`
   - `docker compose version`
2. Если `docker compose` отсутствует, доустановить его до продолжения deploy path.

### Expected result

- сервер готов исполнять `infra/docker-compose.ghostring.yml`

## Phase 3. PostgreSQL Contract

### Stop/go checks

- понятны `host`, `port`, `database`, `username`
- для `dt-auth` есть отдельный application user или согласованный existing user
- проверено, что БД reachable с этого же сервера

### Actions

1. Заполнить `ConnectionStrings__Postgres` в runtime env.
2. Убедиться, что credential не хранится в shell history/documentation notes.
3. Если БД локальная, проверить, что rollout не меняет существующую ownership/backup policy.

### Expected result

- `bootstrap`, `api` и `worker` смогут использовать existing `PostgreSQL` без локального compose `postgres`

## Phase 4. Redis Decision

### Stop/go checks

- выбран отдельный `Redis` именно для `DT-1520`
- понятен способ запуска: контейнер из `docker-compose.ghostring.yml`
- выбран сильный пароль для `OTPAUTH_REDIS_PASSWORD`

### Actions

1. Заполнить `OTPAUTH_REDIS_PASSWORD`.
2. Не пытаться реиспользовать чужой `Redis` без явного решения.

### Expected result

- worker dependency probe по `Redis` сможет стать healthy

## Phase 5. Signing And Protection Keys

### Stop/go checks

- все ключи сгенерированы заранее
- значения не попадут в commit, shell transcript или session note

### Required env keys

- `BootstrapOAuth__CurrentSigningKeyId`
- `BootstrapOAuth__CurrentSigningKey`
- `TotpProtection__CurrentKeyVersion`
- `TotpProtection__CurrentKey`
- `ChallengeCallbacks__SigningKey`
- `Webhooks__SigningKey`

### Actions

1. Заполнить все required signing/protection keys в `runtime.env`.
2. Для pilot оставить `PushDelivery__Provider=logging`, пока runtime не стабилизирован.
3. `FCM` включать только вторым шагом после успешного baseline bring-up.

### Expected result

- `api` и `worker` смогут стартовать fail-closed без runtime config exceptions

## Phase 6. Internal TLS For Admin Container

### Stop/go checks

- есть cert/key, которые host `nginx` сможет доверять
- сертификат соответствует `admin.ghostring.ru` или другому имени, которое осознанно используется в upstream verification

### Actions

1. Подготовить:
   - `OTPAUTH_TLS_CERT_PATH`
   - `OTPAUTH_TLS_KEY_PATH`
2. Проверить права доступа на файлы.
3. Убедиться, что host `nginx` сможет пройти upstream TLS verification.

### Expected result

- `admin` container слушает HTTPS на loopback-порту и не требует permissive `proxy_ssl_verify off`

## Phase 7. Host Nginx Site Preparation

### Stop/go checks

- `admin.ghostring.ru` резолвится на этот сервер
- host `nginx` уже управляется безопасно и изменение делается как отдельный site config
- порт `443` уже обслуживается existing `nginx`, а не новым контейнером

### Actions

1. Взять за основу `infra/nginx/admin.ghostring.ru.conf.example`.
2. Подготовить site config без изменения unrelated virtual hosts.
3. Проверить конфигурацию:
   - `nginx -t`
4. Не применять конфиг до готовности runtime env и internal TLS.

### Expected result

- host-level reverse proxy готов, но rollout можно делать controlled order-ом

## Phase 8. Compose File Review

### Stop/go checks

- используется именно `infra/docker-compose.ghostring.yml`
- никто не собирается запускать default `infra/docker-compose.yml`

### Review points

- `postgres` отсутствует
- `admin` опубликован только как `127.0.0.1:${OTPAUTH_GHOSTRING_ADMIN_HTTPS_PORT:-18443}:8443`
- memory limits соответствуют pilot profile
- `bootstrap`, `api`, `worker` используют existing `ConnectionStrings__Postgres`

### Expected result

- rollout path соответствует принятому `ghostring` profile, а не полному default contour

## Phase 9. Bootstrap Readiness

### Stop/go checks

- `runtime.env` заполнен полностью
- compose file читается без пустых required variables
- есть решение по bootstrap admin username/permissions
- есть process-level `OTPAUTH_ADMIN_PASSWORD` только на момент bootstrap

### Actions

1. Подготовить bootstrap sequence:
   - `ensure-database`
   - `migrate`
   - `upsert-admin-user`
2. Не выполнять его до завершения всех предыдущих фаз.

### Expected result

- сервер готов к controlled first bootstrap без ad-hoc решений на месте

## Phase 10. Smoke Check Plan Before First Real Deploy

### Prepare checks in advance

- `docker compose ps`
- health у `redis`
- health у `worker`
- `https://127.0.0.1:18443/health/api` на хосте
- `https://admin.ghostring.ru/health/api` после включения host `nginx`
- operator login в `Admin UI`

### Failure policy

- если `worker` unhealthy, не идти дальше к public ingress
- если upstream TLS не проходит, не выключать verification ради speed-up
- если `api/admin` не стартуют из-за config gaps, исправлять env/keys, а не править code/compose ad-hoc

## Immediate continuation point

Когда этот checklist закрыт, следующий шаг уже operational:

1. заполнить `/opt/dt-1520-authenticator/runtime.env`
2. подготовить internal TLS files
3. установить `docker compose`, если его еще нет
4. проверить `nginx` site config
5. запускать controlled bootstrap/deploy path
