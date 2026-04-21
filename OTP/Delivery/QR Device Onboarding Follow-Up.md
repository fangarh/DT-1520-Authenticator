# QR Device Onboarding Follow-Up

## Status

Accepted discovery and follow-up task

## Goal

Подготовить и затем реализовать полноценный QR-based onboarding flow для мобильного приложения, чтобы оператор мог безопасно передавать пользователю одноразовый activation artifact без ручного копирования кодов.

## Why this matters

Текущий `Device Registry` уже имеет server-side activation artifact model через `auth.device_activation_codes`, но operator-ready UX для выдачи такого артефакта пользователю пока не оформлен.

Без этого:

- onboarding устройства остается техническим, а не productized
- первый `push` pilot зависит от ручной подготовки activation path
- mobile rollout сложнее объяснять и воспроизводить support/operator-ам

## Target outcome

После закрытия этой задачи система должна уметь:

- сгенерировать одноразовый device onboarding artifact из admin/operator surface
- представить его пользователю в QR-friendly форме
- позволить mobile app считать QR и завершить activation flow
- автоматически погасить artifact после успешного consume
- не оставлять reusable onboarding links после scan/activation

## Current design premise

На текущий момент наиболее вероятная каноническая модель такая:

- `Admin UI` или операторский backend flow создает one-time activation artifact
- artifact имеет TTL и server-side status
- QR кодирует не долгоживущую ссылку, а короткоживущий opaque activation payload
- mobile app сканирует QR и вызывает existing activation path
- server consume-ит artifact атомарно в activation flow
- отдельная post-scan команда "удалить ссылку" не должна быть единственным механизмом инвалидирования

## Discussed result

Результат обсуждения зафиксирован так:

- оператор в `Admin UI` инициирует `Выдать QR для подключения устройства`
- backend создает одноразовый onboarding artifact поверх existing `auth.device_activation_codes`
- artifact должен быть привязан как минимум к `tenantId`, `applicationClientId`, `externalUserId`, иметь `TTL` и при необходимости `platform`
- `Admin UI` показывает QR пользователю
- mobile app сканирует QR и использует existing `POST /api/v1/devices/activate`
- server атомарно consume-ит artifact внутри activation flow
- после успешной активации artifact автоматически становится недействительным

Для первого implementation slice предпочтение отдано `opaque activation payload`, а не deep-link-first подходу.

Первичный security contract фиксируется server-side:

- `one-time`
- `expiresAt`
- `consumedAt`
- optional `revokedAt`

Команда со стороны телефона на "отключение/удаление ссылки" допустима только как вторичный UX signal. Она не должна быть единственным механизмом защиты и не должна подменять server-side invalidation semantics.

## Important design caution

Идея "после сканирования телефон передает команду об отключении/удалении ссылки" сама по себе недостаточна как основной security mechanism.

Почему:

- scan еще не означает успешный activation
- app может упасть после scan и до confirm
- link может быть просканирован, но не завершен
- отдельная `delete link` команда создает race и лишнюю точку отказа

Поэтому базовый security contract должен быть server-side:

- artifact либо consume-ится атомарно в `activate`
- либо истекает по TTL
- либо явно revoke-ится оператором

А post-scan invalidation со стороны телефона можно рассматривать только как дополнительный UX signal, но не как единственный security barrier.

## Open design questions

Перед реализацией нужно согласовать:

1. Что именно кодируется в QR:
   - first-choice: opaque activation payload
   - альтернативно: короткая HTTPS link
   - отдельно, только если понадобится: custom app link / deep link
2. Где генерируется QR:
   - в `Admin UI`
   - на backend с уже готовым QR payload
3. Какой UX нужен оператору:
   - показать QR на экране
   - выдать короткую ссылку
   - распечатать / передать изображение
4. Какой TTL нужен:
   - минуты
   - часы
5. Нужен ли explicit revoke path для уже созданного, но не использованного artifact
6. Нужно ли связывать artifact с:
   - `tenantId`
   - `applicationClientId`
   - `externalUserId`
   - platform
7. Нужен ли single-device intent per artifact или допустим повторный выпуск нового QR для того же пользователя без revoke старого

## Minimum implementation expectations

Когда задача пойдет в реализацию, минимум должен включать:

- admin/backend flow для create/list/revoke pending onboarding artifacts
- QR-friendly payload contract
- mobile scanning/import path
- atomарный consume в device activation flow
- automated tests
- security review

## Continuation point

Эту задачу сначала нужно обсудить и утвердить на уровне flow/contract.

Только после этого ее можно переводить в implementation track.
