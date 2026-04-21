import type { AdminUserDeviceStatus } from "../../shared/types/admin-contracts";
import styles from "./UserDeviceStatusBadge.module.css";

interface UserDeviceStatusBadgeProps {
  status: AdminUserDeviceStatus;
}

export function UserDeviceStatusBadge({ status }: UserDeviceStatusBadgeProps) {
  return (
    <span className={[styles.badge, styles[status]].join(" ")}>
      {status}
    </span>
  );
}
