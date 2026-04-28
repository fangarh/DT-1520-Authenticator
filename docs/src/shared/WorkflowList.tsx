import type { Workflow } from "../data/documentation";
import styles from "./WorkflowList.module.css";

interface WorkflowListProps {
  workflows: Workflow[];
}

export function WorkflowList({ workflows }: WorkflowListProps) {
  return (
    <section className={styles.wrap} id="workflows" aria-labelledby="workflows-title">
      <div className={styles.heading}>
        <span>operator workflows</span>
        <h2 id="workflows-title">Рабочие процессы оператора</h2>
      </div>
      <div className={styles.list}>
        {workflows.map((workflow) => (
          <article className={styles.card} key={workflow.title}>
            <div>
              <h3>{workflow.title}</h3>
              <p className={styles.path}>{workflow.path}</p>
            </div>
            <p className={styles.permission}>{workflow.permission}</p>
            <ol>
              {workflow.steps.map((step) => (
                <li key={`${workflow.title}-${step}`}>{step}</li>
              ))}
            </ol>
            <p className={styles.audit}>{workflow.audit}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
