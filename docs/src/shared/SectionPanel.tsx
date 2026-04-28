import type { DocumentationSection } from "../data/documentation";
import styles from "./SectionPanel.module.css";

interface SectionPanelProps {
  section: DocumentationSection;
}

export function SectionPanel({ section }: SectionPanelProps) {
  return (
    <section className={styles.panel} id={section.id} aria-labelledby={`${section.id}-title`}>
      <div className={styles.header}>
        <span className={styles.kicker}>{section.id}</span>
        <h2 id={`${section.id}-title`}>{section.title}</h2>
        <p>{section.lead}</p>
      </div>
      <div className={styles.grid}>
        {section.items.map((item) => (
          <article className={styles.item} key={`${section.id}-${item.label}`}>
            <h3>{item.label}</h3>
            <p>{item.detail}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
