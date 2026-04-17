# On-Prem Delivery

## Цель

Поддержать коробочную поставку без обязательной зависимости от облачного runtime.

Fully air-gapped профиль требуется в дальнейшем как enterprise-направление, но не входит в `MVP`.

## Формы поставки

### Base

- `Docker Compose`
- один backend instance
- `PostgreSQL`
- `Redis`

### Enterprise

- `Kubernetes`
- `Helm`
- `HA PostgreSQL`
- отказоустойчивый `Redis`
- внешний `Vault/KMS`

## Bootstrap и runtime

Для коробочной поставки разделяются два контура:

- `runtime plane`: `Api`, `Worker`, `Admin UI`, `PostgreSQL`, `Redis`
- `bootstrap/setup plane`: installer или локальный `Bootstrap Agent`, который проверяет окружение, сохраняет конфигурацию и поднимает runtime

`Admin UI` не должна сама получать host-level права на запуск backend-сервисов или запись секретов в runtime-конфигурацию.

## Рекомендуемый порядок delivery

1. Сначала зафиксировать architecture/design setup plane.
2. Затем реализовать runtime `Admin UI` поверх готового enrollment management backend.
3. Затем реализовать `Installer MVP` перед первым реальным `on-prem` rollout.

## Критичные требования

- `AD/LDAP`
- резервное копирование
- `SIEM` export
- внутренние сертификаты
- обновление без потери enrollments и секретов

## Ограничение по push

Если заказчику нужен полностью изолированный контур, `push` может стать опциональным, а `TOTP` должен оставаться рабочим первым выбором для offline-сценариев.

Даже в обычном `on-prem` нельзя предполагать, что `APNs/FCM` доступны во всех инсталляциях.
