import type { AdminDeliveryStatus } from "../../shared/types/admin-contracts";
import styles from "./DeliveryStatusBadge.module.css";

interface DeliveryStatusBadgeProps {
  status: AdminDeliveryStatus;
}

export function DeliveryStatusBadge({ status }: DeliveryStatusBadgeProps) {
  return (
    <span className={[styles.badge, styles[status]].join(" ")}>
      {status}
    </span>
  );
}
