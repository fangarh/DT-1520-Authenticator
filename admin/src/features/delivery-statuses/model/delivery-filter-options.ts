import type { AdminDeliveryChannel, AdminDeliveryStatus } from "../../../shared/types/admin-contracts";

export const deliveryChannelOptions: Array<{ value: AdminDeliveryChannel; label: string }> = [
  { value: "challenge_callback", label: "challenge_callback" },
  { value: "webhook_event", label: "webhook_event" },
];

export const deliveryStatusOptions: Array<{ value: AdminDeliveryStatus; label: string }> = [
  { value: "queued", label: "queued" },
  { value: "delivered", label: "delivered" },
  { value: "failed", label: "failed" },
];
