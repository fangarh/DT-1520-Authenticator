import styles from "./Notice.module.css";

interface NoticeProps {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

export function Notice({ tone, title, detail, actionHint }: NoticeProps) {
  return (
    <div className={[styles.notice, styles[tone]].join(" ")}>
      <strong>{title}</strong>
      <span>{detail}</span>
      {actionHint ? <small>{actionHint}</small> : null}
    </div>
  );
}
