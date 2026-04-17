import type { PropsWithChildren, ReactNode } from "react";
import styles from "./Panel.module.css";

interface PanelProps {
  title: string;
  eyebrow?: string;
  aside?: ReactNode;
}

export function Panel({ title, eyebrow, aside, children }: PropsWithChildren<PanelProps>) {
  return (
    <section className={styles.panel}>
      <header className={styles.header}>
        <div>
          {eyebrow ? <p className={styles.eyebrow}>{eyebrow}</p> : null}
          <h2 className={styles.title}>{title}</h2>
        </div>
        {aside ? <div className={styles.aside}>{aside}</div> : null}
      </header>
      <div className={styles.body}>{children}</div>
    </section>
  );
}
