import { quickStartCommands, repoRoots, sections, troubleshooting, workflows } from "../data/documentation";
import { SectionPanel } from "../shared/SectionPanel";
import { WorkflowList } from "../shared/WorkflowList";
import styles from "./App.module.css";

export function App() {
  return (
    <div className={styles.page}>
      <header className={styles.hero}>
        <nav className={styles.nav} aria-label="Разделы документации">
          <a href="#overview">Обзор</a>
          <a href="#setup">Запуск</a>
          <a href="#admin">Admin UI</a>
          <a href="#android">Android</a>
          <a href="#security">Security</a>
        </nav>
        <div className={styles.heroGrid}>
          <div className={styles.heroText}>
            <p className={styles.eyebrow}>DT-1520 Authenticator</p>
            <h1>Документация MVP runtime</h1>
            <p>
              Статическая React-страница для передачи контекста по backend, Admin UI,
              Android factor app, installer contour, SDK roadmap и security boundaries.
            </p>
          </div>
          <div className={styles.systemMap} aria-label="Карта runtime контуров">
            {repoRoots.map((root) => (
              <span key={root}>{root}</span>
            ))}
          </div>
        </div>
      </header>

      <main className={styles.main}>
        <section className={styles.quickStart} aria-labelledby="quick-start-title">
          <div>
            <span className={styles.eyebrow}>first handoff</span>
            <h2 id="quick-start-title">Минимальный старт</h2>
            <p>
              Команды ниже не содержат реальные секреты. Значения в угловых скобках должны
              задаваться вне репозитория и не попадать в историю.
            </p>
          </div>
          <pre aria-label="Команды первого запуска">
            <code>{quickStartCommands.join("\n")}</code>
          </pre>
        </section>

        {sections.map((section) => (
          <SectionPanel section={section} key={section.id} />
        ))}

        <WorkflowList workflows={workflows} />

        <section className={styles.troubleshooting} id="troubleshooting" aria-labelledby="troubleshooting-title">
          <div>
            <span className={styles.eyebrow}>operations</span>
            <h2 id="troubleshooting-title">Troubleshooting</h2>
          </div>
          <ul>
            {troubleshooting.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>
      </main>
    </div>
  );
}
