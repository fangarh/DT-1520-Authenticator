import { useState } from "react";
import { Button } from "../../shared/ui/Button";
import { Panel } from "../../shared/ui/Panel";
import styles from "./LoginPanel.module.css";

interface LoginPanelProps {
  isBootstrapping: boolean;
  isSubmitting: boolean;
  error: string | null;
  onSubmit: (username: string, password: string) => Promise<void>;
}

export function LoginPanel({ isBootstrapping, isSubmitting, error, onSubmit }: LoginPanelProps) {
  const [username, setUsername] = useState("operator");
  const [password, setPassword] = useState("super-secret");

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSubmit(username, password);
  }

  return (
    <Panel
      eyebrow="Auth"
      title={isBootstrapping ? "Restoring admin session..." : "Sign in to the operator contour"}
    >
      <form className={styles.form} onSubmit={handleSubmit}>
        <label className={styles.field}>
          <span>Username</span>
          <input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" />
        </label>

        <label className={styles.field}>
          <span>Password</span>
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
          />
        </label>

        <Button type="submit" stretch disabled={isBootstrapping || isSubmitting}>
          {isBootstrapping ? "Bootstrapping..." : isSubmitting ? "Signing in..." : "Open workspace"}
        </Button>

        {error ? <p className={styles.error}>{error}</p> : null}
      </form>
    </Panel>
  );
}
