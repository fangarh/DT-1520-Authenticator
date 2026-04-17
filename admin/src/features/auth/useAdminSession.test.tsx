import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, afterEach } from "vitest";
import { useAdminSession } from "./useAdminSession";

const { getSessionMock } = vi.hoisted(() => ({
  getSessionMock: vi.fn(),
}));

vi.mock("../../shared/api/admin-api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../shared/api/admin-api")>();

  return {
    ...actual,
    adminApi: {
      ...actual.adminApi,
      getSession: getSessionMock,
      login: vi.fn(),
      logout: vi.fn(),
    },
  };
});

function SessionProbe() {
  const session = useAdminSession();

  return (
    <div>
      <span data-testid="status">{session.status}</span>
      <span data-testid="error">{session.error ?? ""}</span>
    </div>
  );
}

describe("useAdminSession", () => {
  afterEach(() => {
    getSessionMock.mockReset();
  });

  it("does not re-bootstrap session after a failed request updates state", async () => {
    getSessionMock.mockRejectedValue(new Error("backend exploded"));

    render(<SessionProbe />);

    await waitFor(() => {
      expect(screen.getByTestId("status").textContent).toBe("anonymous");
    });

    expect(screen.getByTestId("error").textContent).toContain("backend exploded");
    expect(getSessionMock).toHaveBeenCalledTimes(1);
  });
});
