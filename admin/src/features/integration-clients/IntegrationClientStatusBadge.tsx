import type { AdminIntegrationClientStatus } from "../../shared/types/admin-contracts";
import styles from "./IntegrationClientStatusBadge.module.css";

interface IntegrationClientStatusBadgeProps {
  status: AdminIntegrationClientStatus;
}

export function IntegrationClientStatusBadge({ status }: IntegrationClientStatusBadgeProps) {
  return (
    <span className={[styles.badge, styles[status]].join(" ")}>
      {status}
    </span>
  );
}
