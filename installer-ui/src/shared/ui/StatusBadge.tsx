import styles from "./StatusBadge.module.css";

interface StatusBadgeProps {
  tone: "success" | "warning" | "danger" | "neutral";
  label: string;
}

export function StatusBadge({ tone, label }: StatusBadgeProps) {
  return <span className={[styles.badge, styles[tone]].join(" ")}>{label}</span>;
}
