# Ghostring Pilot Deployment Profile

## Status

Accepted working note

## Goal

Зафиксировать безопасный deployment profile для первого внешнего server-side rollout `DT-1520 Authenticator` на личном сервере `ghostring`.

Этот note не заменяет общий `Installer MVP` contract и не объявляет машину production-grade target. Он фиксирует, что сервер признан пригодным именно для pilot rollout с малой нагрузкой и с более узким runtime contour, чем default `infra/docker-compose.yml`.

## Server suitability reassessment

На повторной оценке сервер признан пригодным для pilot deployment при следующих вводных:

- ожидаемая нагрузка не более `10` пользователей
- доступен отдельный DNS `admin.ghostring.ru`
- разрешена установка `docker compose`
- уже существует собственная БД, ранее использовавшаяся как `dt-auth`
- сервер контролируется владельцем и допускает controlled changes в пределах нового приложения

## Current server facts

- ОС: `Ubuntu 24.04.4 LTS`
- runtime: `Docker` доступен, `docker compose` можно доустановить
- ingress: на хосте уже работает `nginx`
- TLS: есть действующий `Let's Encrypt`
- уже заняты `80/443/8443`, поэтому public exposure нового runtime должен идти через existing host `nginx`
- ресурсы ограничены: `2 vCPU`, около `2 GiB RAM`
- локальный `postgresql` уже существует и является предпочтительным data store для pilot

## Accepted pilot deployment profile

Для первого rollout принимается такой профиль:

- `api`
- `worker`
- `admin`
- existing host `nginx` как public HTTPS edge
- existing `PostgreSQL` / `dt-auth`
- отдельный `Redis` runtime для `DT-1520`

Что сознательно не делаем в первом rollout:

- не поднимаем compose-managed `postgres`
- не публикуем `admin` напрямую наружу на `8443`
- не трогаем existing workloads вне нового upstream/контейнеров
- не используем full default installer path как black-box deploy без server-specific override

## Public exposure model

- public host: `admin.ghostring.ru`
- TLS termination: existing host `nginx`
- `admin` container слушает только internal port / docker network
- host `nginx` reverse-proxy-ит `admin.ghostring.ru` на новый runtime contour

Это означает, что checked-in `infra/nginx/admin.conf` и default `8443` exposure не являются каноническим public ingress для этого сервера.

## Data and dependency model

### PostgreSQL

- использовать existing `dt-auth` database или отдельную БД в том же managed/hosted `PostgreSQL`
- новый compose-managed `postgres` для пилота не нужен

### Redis

Для первого rollout нужен отдельный `Redis`, но не как shared dependency чужой инфраструктуры.

Предпочтительный вариант:

- отдельный lightweight `redis` instance/container для `DT-1520`

Причина:

- `Deployment Topology MVP` и current runtime предполагают `Redis` для rate limiting, anti-replay и runtime throttling
- `worker` diagnostics ожидают валидный `Redis:ConnectionString`

## Resource posture

Сервер остается небольшим, поэтому pilot runtime обязан идти с ограничениями по памяти и без лишних infra units.

Рекомендованные стартовые лимиты:

- `api`: `384M-512M`
- `worker`: `256M-384M`
- `admin`: `96M-128M`
- `redis`: `64M-128M`

Эти значения не считаются финальными performance guarantees; они фиксируют безопасную стартовую рамку для controlled pilot.

## Deployment recommendation

Первый безопасный rollout следует готовить как server-specific compose profile:

- reuse existing images / Dockerfiles
- override default `docker-compose.yml`
- remove local `postgres`
- keep `admin` internal-only
- bind/public ingress решать через host `nginx`
- хранить runtime env вне репозитория

Подготовленные preparatory assets:

- `infra/docker-compose.ghostring.yml`
- `infra/env/ghostring.runtime.env.example`
- `infra/nginx/admin.ghostring.ru.conf.example`

## Required prep before deploy

1. Установить `docker compose`.
2. Подготовить server-specific compose override для `ghostring`.
3. Подготовить runtime env file вне репозитория.
4. Определить отдельный `Redis` для `DT-1520`.
5. Подготовить `nginx` upstream/site config для `admin.ghostring.ru`.
6. Зафиксировать rollout order и smoke checks до первого запуска.

## Non-goals for prep

- не выполнять deploy прямо из этого note
- не редактировать существующий host `nginx` ad-hoc без подготовленного rollout plan
- не включать в первый проход дополнительный hardening beyond pilot minimum

## Continuation point

Следующий практический шаг после этой фиксации:

- заполнить runtime env и подготовить internal TLS material
- установить `docker compose`, если он еще не установлен
- выполнить controlled pre-deploy checklist и только потом переходить к реальному rollout
