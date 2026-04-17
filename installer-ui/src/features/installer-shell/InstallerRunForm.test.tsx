import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { InstallerRunForm } from "./InstallerRunForm";
import { createInstallerRunDraft, type InstallerRunDraft } from "./model/installer-run-draft";

function renderForm(draft: InstallerRunDraft, onDraftChange = vi.fn()) {
  return render(
    <InstallerRunForm
      draft={draft}
      loadingShellInfo={false}
      pending={false}
      shellInfo={{
        transport: "loopback-http",
        host: "127.0.0.1",
        port: 4180,
        executionMode: "mock",
        defaultComposeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
      }}
      onDraftChange={onDraftChange}
      onPermissionEnabled={vi.fn()}
      onSubmit={vi.fn()}
    />,
  );
}

describe("InstallerRunForm", () => {
  it("shows the bootstrap admin password field for install mode", () => {
    renderForm(createInstallerRunDraft(null));

    expect(screen.getByLabelText("Bootstrap admin password")).toBeTruthy();
  });

  it("blocks live install submit until the required guardrails are satisfied", () => {
    const draft = {
      ...createInstallerRunDraft(null),
      envFilePath: "C:\\secure\\otpauth\\runtime.env",
    };

    renderForm(draft);

    const button = screen.getByRole("button", { name: "Run install flow" }) as HTMLButtonElement;

    expect(button.disabled).toBe(true);
    expect(screen.getByText(/bootstrap admin password is required for a live install/i)).toBeTruthy();
  });

  it("switches to password handling hint when bootstrap admin upsert is skipped", async () => {
    const user = userEvent.setup();
    let currentDraft = createInstallerRunDraft(null);
    const view = renderForm(currentDraft);

    const onDraftChange = vi.fn((updater: (current: InstallerRunDraft) => InstallerRunDraft) => {
      currentDraft = updater(currentDraft);
      view.rerender(
        <InstallerRunForm
          draft={currentDraft}
          loadingShellInfo={false}
          pending={false}
          shellInfo={{
            transport: "loopback-http",
            host: "127.0.0.1",
            port: 4180,
            executionMode: "mock",
            defaultComposeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
          }}
          onDraftChange={onDraftChange}
          onPermissionEnabled={vi.fn()}
          onSubmit={vi.fn()}
        />,
      );
    });

    view.rerender(
      <InstallerRunForm
        draft={currentDraft}
        loadingShellInfo={false}
        pending={false}
        shellInfo={{
          transport: "loopback-http",
          host: "127.0.0.1",
          port: 4180,
          executionMode: "mock",
          defaultComposeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
        }}
        onDraftChange={onDraftChange}
        onPermissionEnabled={vi.fn()}
        onSubmit={vi.fn()}
      />,
    );
    await user.click(screen.getByLabelText("Skip bootstrap admin upsert"));

    expect(screen.queryByLabelText("Bootstrap admin password")).toBeNull();
    expect(screen.getByText(/does not need bootstrap admin credentials/i)).toBeTruthy();
  });

  it("shows recover-specific mode guidance", async () => {
    const user = userEvent.setup();
    let currentDraft = {
      ...createInstallerRunDraft(null),
      envFilePath: "C:\\secure\\otpauth\\runtime.env",
      bootstrapAdminPassword: "strong-password",
    };
    const view = renderForm(currentDraft);

    const onDraftChange = vi.fn((updater: (current: InstallerRunDraft) => InstallerRunDraft) => {
      currentDraft = updater(currentDraft);
      view.rerender(
        <InstallerRunForm
          draft={currentDraft}
          loadingShellInfo={false}
          pending={false}
          shellInfo={{
            transport: "loopback-http",
            host: "127.0.0.1",
            port: 4180,
            executionMode: "mock",
            defaultComposeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
          }}
          onDraftChange={onDraftChange}
          onPermissionEnabled={vi.fn()}
          onSubmit={vi.fn()}
        />,
      );
    });

    view.rerender(
      <InstallerRunForm
        draft={currentDraft}
        loadingShellInfo={false}
        pending={false}
        shellInfo={{
          transport: "loopback-http",
          host: "127.0.0.1",
          port: 4180,
          executionMode: "mock",
          defaultComposeFilePath: "D:\\Projects\\2026\\DT-1520-Authenticator\\infra\\docker-compose.yml",
        }}
        onDraftChange={onDraftChange}
        onPermissionEnabled={vi.fn()}
        onSubmit={vi.fn()}
      />,
    );

    await user.click(screen.getByLabelText("Recover"));

    expect(screen.getByText(/fast recovery after restart or partial container failure/i)).toBeTruthy();
    const button = screen.getByRole("button", { name: "Run recover flow" }) as HTMLButtonElement;

    expect(button.disabled).toBe(false);
  });
});
