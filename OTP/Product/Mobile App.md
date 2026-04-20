# Mobile App

## Роль

Мобильное приложение является контейнером второго фактора и должно поддерживать два режима:

- `TOTP`
- `Push approval`

## Функции первой очереди

- генерация `TOTP`
- получение `push`-запросов
- `approve / deny`
- локальная биометрия перед подтверждением
- базовая история последних подтверждений

## Сценарий привязки

1. Пользователь проходит первый фактор.
2. Запускается enrollment.
3. Сервер выдает `QR` или activation code.
4. Приложение привязывает устройство.
5. Устройство появляется в `Device Registry`.

Для закрытого `Android TOTP-first` слайса канонический provisioning contract зафиксирован в [[../Integrations/TOTP Provisioning Contract]].

План и checkpoint-ы `Android Push Runtime`, которым был закрыт следующий mobile push track после `TOTP-first`, зафиксированы в [[Android Push Runtime Plan]].

На `2026-04-17` `Android Push Runtime` закрыт через `Iteration 1-4`: реализованы pending inbox, secure device session storage, transport для `activate/refresh/pending/approve/deny`, локальный `BiometricPrompt` gate перед approve и encrypted sanitized history последних решений.

Следующий mobile/product шаг больше не в закрытии самого push runtime, а в смежных enrollment/support/ops сценариях, если они будут подтверждены отдельным продуктовым решением.

## Безопасность

- `Keychain / Keystore`
- локальная защита `PIN/biometric`
- отзыв устройства
- offline fallback через `TOTP`

## Технологический выбор

Для `MVP` выбран `Kotlin` и нативный `Android`-клиент.

`iPhone` не входит в обязательный первый контур и переносится в поздний этап после стабилизации `Android` и backend.

Bootstrap-параметры проекта зафиксированы в [[Android App Bootstrap]].

Для планирования старта работ по `admin UI` и `Android` readiness gates зафиксированы в [[../Delivery/Admin and Android Readiness Gates]].

Исторический пошаговый трек, через который закрывался локальный `TOTP-first` slice, зафиксирован в [[Android TOTP-First Plan]].
