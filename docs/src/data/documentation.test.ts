import { describe, expect, it } from "vitest";
import { quickStartCommands, sections, troubleshooting, workflows } from "./documentation";

describe("documentation content", () => {
  it("covers the required handoff areas", () => {
    expect(sections.map((section) => section.id)).toEqual([
      "overview",
      "setup",
      "deployment",
      "admin",
      "android",
      "security",
      "sdk",
      "roadmap",
    ]);
    expect(workflows).toHaveLength(8);
    expect(workflows.some((workflow) => workflow.title === "Tenant directory")).toBe(true);
    expect(workflows.some((workflow) => workflow.title === "Tenant management")).toBe(true);
  });

  it("keeps examples secret-safe", () => {
    const content = JSON.stringify({ quickStartCommands, sections, troubleshooting, workflows });

    expect(content).not.toContain("client_secret=");
    expect(content).not.toContain("pushToken=");
    expect(content).not.toContain("Password=Host");
    expect(content).toContain("lib/samples/aspnetcore-protected-operation/README.md");
    expect(content).toContain("lib/PRERELEASE-HANDOFF.md");
    expect(content).toContain("<set-a-strong-password>");
  });
});
