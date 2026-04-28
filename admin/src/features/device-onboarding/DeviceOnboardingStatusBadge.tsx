import type { AdminDeviceOnboardingStatus } from "../../shared/types/admin-contracts";
import styles from "./DeviceOnboardingStatusBadge.module.css";

interface DeviceOnboardingStatusBadgeProps {
  status: AdminDeviceOnboardingStatus;
}

export function DeviceOnboardingStatusBadge({ status }: DeviceOnboardingStatusBadgeProps) {
  return <span className={[styles.badge, styles[status]].join(" ")}>{status}</span>;
}
