import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ConfirmEnrollmentForm } from "../enrollment-confirm/ConfirmEnrollmentForm";
import { ReplaceEnrollmentPanel } from "../enrollment-replace/ReplaceEnrollmentPanel";
import { RevokeEnrollmentPanel } from "../enrollment-revoke/RevokeEnrollmentPanel";
import { StartEnrollmentForm } from "../enrollment-start/StartEnrollmentForm";

function StartEnrollmentFormHarness(props: { onSubmit: () => Promise<void> }) {
  const [applicationClientId, setApplicationClientId] = useState("");
  const [issuer, setIssuer] = useState("OTPAuth");
  const [label, setLabel] = useState("");

  return (
    <StartEnrollmentForm
      applicationClientId={applicationClientId}
      issuer={issuer}
      label={label}
      pending={false}
      onApplicationClientIdChange={setApplicationClientId}
      onIssuerChange={setIssuer}
      onLabelChange={setLabel}
      onSubmit={props.onSubmit}
    />
  );
}

function ConfirmEnrollmentFormHarness(props: { disabled: boolean; onSubmit: () => Promise<void> }) {
  const [code, setCode] = useState("");

  return (
    <ConfirmEnrollmentForm
      code={code}
      disabled={props.disabled}
      pending={false}
      onCodeChange={setCode}
      onSubmit={props.onSubmit}
    />
  );
}

describe("Enrollment action panels", () => {
  it("propagates start form edits and submit", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<StartEnrollmentFormHarness onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Application Client ID"), "crm-admin");
    await user.clear(screen.getByLabelText("Issuer"));
    await user.type(screen.getByLabelText("Issuer"), "DT-1520");
    await user.type(screen.getByLabelText("Label"), "alice@corp");
    await user.click(screen.getByRole("button", { name: "Start enrollment" }));

    expect((screen.getByLabelText("Application Client ID") as HTMLInputElement).value).toBe("crm-admin");
    expect((screen.getByLabelText("Issuer") as HTMLInputElement).value).toBe("DT-1520");
    expect((screen.getByLabelText("Label") as HTMLInputElement).value).toBe("alice@corp");
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("disables confirm action when the flow is not available", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<ConfirmEnrollmentFormHarness disabled onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Authenticator code"), "123456");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    const codeInput = screen.getByLabelText("Authenticator code") as HTMLInputElement;
    const confirmButton = screen.getByRole("button", { name: "Confirm" }) as HTMLButtonElement;

    expect(codeInput.value).toBe("123456");
    expect(onSubmit).not.toHaveBeenCalled();
    expect(confirmButton.disabled).toBe(true);
  });

  it("shows replacement safety copy and pending label", () => {
    render(
      <ReplaceEnrollmentPanel
        disabled={false}
        pending
        onSubmit={vi.fn().mockResolvedValue(undefined)}
      />,
    );

    const replaceButton = screen.getByRole("button", { name: "Starting..." }) as HTMLButtonElement;

    expect(screen.getByText(/старый фактор остается активным/i)).toBeTruthy();
    expect(replaceButton.disabled).toBe(true);
  });

  it("shows destructive revoke copy and danger action", () => {
    render(
      <RevokeEnrollmentPanel
        disabled={false}
        pending={false}
        onSubmit={vi.fn().mockResolvedValue(undefined)}
      />,
    );

    expect(screen.getByText(/revoke закрывает текущий enrollment/i)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Revoke enrollment" })).toBeTruthy();
  });
});
