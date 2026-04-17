import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { LoginPanel } from "./LoginPanel";

describe("LoginPanel", () => {
  it("renders bootstrapping state and disables submit", () => {
    render(
      <LoginPanel
        isBootstrapping
        isSubmitting={false}
        error={null}
        onSubmit={vi.fn().mockResolvedValue(undefined)}
      />,
    );

    const submitButton = screen.getByRole("button", { name: "Bootstrapping..." }) as HTMLButtonElement;

    expect(screen.getByRole("heading", { name: "Restoring admin session..." })).toBeTruthy();
    expect(submitButton.disabled).toBe(true);
  });

  it("submits edited credentials through the operator form", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(
      <LoginPanel
        isBootstrapping={false}
        isSubmitting={false}
        error={null}
        onSubmit={onSubmit}
      />,
    );

    const username = screen.getByLabelText("Username");
    const password = screen.getByLabelText("Password");

    await user.clear(username);
    await user.type(username, "alice.operator");
    await user.clear(password);
    await user.type(password, "otp-password");
    await user.click(screen.getByRole("button", { name: "Open workspace" }));

    expect(onSubmit).toHaveBeenCalledOnce();
    expect(onSubmit).toHaveBeenCalledWith("alice.operator", "otp-password");
  });
});
