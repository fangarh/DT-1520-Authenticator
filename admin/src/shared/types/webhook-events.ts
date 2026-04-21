import type { WebhookEventType } from "./admin-contracts";

export const webhookEventOptions: { value: WebhookEventType; label: string; description: string }[] = [
  {
    value: "challenge.approved",
    label: "challenge.approved",
    description: "Терминальный approve по challenge lifecycle.",
  },
  {
    value: "challenge.denied",
    label: "challenge.denied",
    description: "Отрицательное решение по challenge.",
  },
  {
    value: "challenge.expired",
    label: "challenge.expired",
    description: "Истечение challenge без решения.",
  },
  {
    value: "device.activated",
    label: "device.activated",
    description: "Устройство успешно активировано.",
  },
  {
    value: "device.revoked",
    label: "device.revoked",
    description: "Устройство отозвано оператором или системой.",
  },
  {
    value: "device.blocked",
    label: "device.blocked",
    description: "Устройство заблокировано fail-closed path-ом.",
  },
  {
    value: "factor.revoked",
    label: "factor.revoked",
    description: "Фактор TOTP был revoked.",
  },
];
