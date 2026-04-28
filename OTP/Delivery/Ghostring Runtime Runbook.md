# Ghostring Runtime Runbook

## Status

Accepted working note

## Goal

Зафиксировать фактическое состояние живого pilot runtime `DT-1520 Authenticator` на сервере `ghostring` после successful bring-up и первого operator login.

Этот note не заменяет pre-deploy checklist. Он фиксирует текущий working runtime, минимальные smoke-checks и known residuals.

## Current runtime status

На `ghostring` подтвержден working pilot runtime:

- `redis` — healthy
- `api` — up
- `admin` — healthy
- `worker` — healthy

Подтверждено:

- `bootstrap ensure-database`
- `bootstrap migrate`
- `bootstrap upsert-admin-user`
- public `health`
- `csrf-token`
- первый `operator` login

## Public access

Текущий public entrypoint:

- `https://admin.ghostring.ru:18443/`

Текущий public health:

- `https://admin.ghostring.ru:18443/health/api`

Текущий internal health:

- `https://127.0.0.1:18443/health/api`

## Runtime boundary

Host-level runtime root:

- `/opt/dt-1520-authenticator`

Критичные runtime assets:

- `/opt/dt-1520-authenticator/runtime.env`
- `/opt/dt-1520-authenticator/tls/admin-internal.crt`
- `/opt/dt-1520-authenticator/tls/admin-internal.key`

Compose profile:

- `infra/docker-compose.ghostring.yml`

Deterministic runtime network:

- `infra_runtime`
- `OTPAUTH_RUNTIME_NETWORK_CIDR=172.29.152.0/24`

## Trusted proxy posture

Admin auth contour работает за reverse proxy chain без ослабления security policy:

- `Secure` cookies сохранены
- `CSRF` сохранен
- `OtpAuth.Api` принимает `X-Forwarded-Proto/X-Forwarded-For` только от trusted runtime network
- trust-all proxy режим не используется

## Current operator account

Bootstrap operator:

- `username=operator`

Подтвержденные permissions:

- `devices.read`
- `devices.write`
- `enrollments.read`
- `enrollments.write`
- `webhooks.read`
- `webhooks.write`

## Minimal smoke checks

### Docker status

```powershell
docker compose --env-file /opt/dt-1520-authenticator/runtime.env -f infra/docker-compose.ghostring.yml ps
```

### Internal health

```powershell
curl -sk https://127.0.0.1:18443/health/api
```

### Public health

```powershell
curl -sk https://admin.ghostring.ru:18443/health/api
```

### CSRF

```powershell
curl -iks https://admin.ghostring.ru:18443/api/v1/admin/auth/csrf-token
```

### Operator session

Проверка уже подтверждена на живом runtime:

- `GET csrf-token -> 200`
- `POST login -> 200`
- `GET session -> 200`

## Operational residuals

Открытые residuals после successful bring-up:

- public ingress работает на `:18443`, а не на `443`
- `443` на этом IP остается занят existing `ocserv`
- `api` и `worker` логируют отсутствие `libgssapi_krb5.so.2`, но runtime остается healthy и функционально рабочим

Это не считается blocker-ом для pilot usage, но должно быть сохранено как post-bring-up hardening backlog.

## Practical meaning

Server-side contour считается готовым для:

- operator work через `Admin UI`
- следующего шага `ProjectManager` pilot integration

Следующий practical step теперь уже не infra-first:

- использовать этот runtime как live target для `ProjectManager` step-up MFA integration
