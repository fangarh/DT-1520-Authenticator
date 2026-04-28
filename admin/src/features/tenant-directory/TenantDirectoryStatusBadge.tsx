import type { AdminTenantDirectoryStatus } from "../../shared/types/admin-contracts";
import styles from "./TenantDirectoryStatusBadge.module.css";

interface TenantDirectoryStatusBadgeProps {
  status: AdminTenantDirectoryStatus;
}

export function TenantDirectoryStatusBadge({ status }: TenantDirectoryStatusBadgeProps) {
  return (
    <span className={[styles.badge, styles[status]].join(" ")}>
      {status}
    </span>
  );
}
