import type { TotpEnrollmentStatus } from "../types/admin-contracts";
import styles from "./StatusBadge.module.css";

interface StatusBadgeProps {
  status: TotpEnrollmentStatus;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  return (
    <span className={[styles.badge, styles[status]].join(" ")}>
      {status}
    </span>
  );
}
