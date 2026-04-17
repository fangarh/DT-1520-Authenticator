import { describe, expect, it } from "vitest";
import { mapAdminError, mapAdminProblem } from "./problem-messages";

describe("mapAdminProblem", () => {
  it("maps multi-client conflict to application client hint", () => {
    const message = mapAdminProblem({
      status: 409,
      detail: "Tenant 'x' has multiple active application clients. Provide ApplicationClientId explicitly.",
    });

    expect(message.title).toBe("Нужен application client");
    expect(message.actionHint).toContain("applicationClientId");
  });

  it("maps confirmation lock conflict to restart hint", () => {
    const message = mapAdminProblem({
      status: 409,
      detail: "Too many invalid replacement confirmation attempts. Restart replacement.",
    });

    expect(message.title).toBe("Лимит попыток исчерпан");
    expect(message.actionHint).toContain("replacement");
  });

  it("maps unauthorized response to re-login guidance", () => {
    const message = mapAdminProblem({ status: 401, detail: "Authentication failed." });

    expect(message.title).toBe("Сессия недействительна");
    expect(message.actionHint).toContain("авторизуйтесь");
  });

  it("maps transport failure to backend availability guidance", () => {
    const message = mapAdminError(new Error("fetch failed"));

    expect(message.title).toBe("Связь с backend недоступна");
    expect(message.detail).toContain("fetch failed");
  });
});
