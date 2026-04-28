import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("App", () => {
  it("renders the documentation landing page", () => {
    render(<App />);

    expect(screen.getByRole("heading", { name: "Документация MVP runtime" })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "Разделы документации" })).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { name: "Admin UI" }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("heading", { name: ".NET SDK" }).length).toBeGreaterThan(0);
    expect(screen.getByText("enrollments.read + enrollments.write")).toBeInTheDocument();
  });
});
