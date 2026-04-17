import type { ButtonHTMLAttributes, PropsWithChildren } from "react";
import styles from "./Button.module.css";

type ButtonKind = "primary" | "secondary" | "ghost";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  kind?: ButtonKind;
  stretch?: boolean;
}

export function Button({
  kind = "primary",
  stretch = false,
  className,
  children,
  ...props
}: PropsWithChildren<ButtonProps>) {
  return (
    <button
      {...props}
      className={[styles.button, styles[kind], stretch ? styles.stretch : "", className ?? ""].filter(Boolean).join(" ")}
    >
      {children}
    </button>
  );
}
