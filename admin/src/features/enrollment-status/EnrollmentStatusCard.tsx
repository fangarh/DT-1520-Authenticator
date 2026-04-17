import { Panel } from "../../shared/ui/Panel";
import { StatusBadge } from "../../shared/ui/StatusBadge";
import type { TotpEnrollmentCurrent } from "../../shared/types/admin-contracts";
import styles from "./EnrollmentStatusCard.module.css";

interface EnrollmentStatusCardProps {
  enrollment: TotpEnrollmentCurrent | null;
}

export function EnrollmentStatusCard({ enrollment }: EnrollmentStatusCardProps) {
  return (
    <Panel
      eyebrow="Status"
      title="Operator decision card"
      aside={enrollment ? <StatusBadge status={enrollment.status} /> : null}
    >
      {enrollment ? (
        <dl className={styles.grid}>
          <div>
            <dt>Enrollment ID</dt>
            <dd>{enrollment.enrollmentId}</dd>
          </div>
          <div>
            <dt>Application Client ID</dt>
            <dd>{enrollment.applicationClientId}</dd>
          </div>
          <div>
            <dt>External User</dt>
            <dd>{enrollment.externalUserId}</dd>
          </div>
          <div>
            <dt>Pending replacement</dt>
            <dd>{enrollment.hasPendingReplacement ? "yes" : "no"}</dd>
          </div>
        </dl>
      ) : (
        <p className={styles.empty}>Current enrollment появится здесь после lookup.</p>
      )}
    </Panel>
  );
}
