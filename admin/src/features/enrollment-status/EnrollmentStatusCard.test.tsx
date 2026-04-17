import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EnrollmentStatusCard } from "./EnrollmentStatusCard";

describe("EnrollmentStatusCard", () => {
  it("renders empty lookup state without enrollment details", () => {
    render(<EnrollmentStatusCard enrollment={null} />);

    expect(screen.getByText("Current enrollment появится здесь после lookup.")).toBeTruthy();
    expect(screen.queryByText("Enrollment ID")).toBeNull();
  });

  it("renders sanitized current enrollment summary", () => {
    render(
      <EnrollmentStatusCard
        enrollment={{
          enrollmentId: "enr-1",
          tenantId: "tenant-a",
          applicationClientId: "crm-web",
          externalUserId: "alice",
          status: "confirmed",
          hasPendingReplacement: true,
          confirmedAtUtc: null,
          revokedAtUtc: null,
        }}
      />,
    );

    expect(screen.getByText("enr-1")).toBeTruthy();
    expect(screen.getByText("crm-web")).toBeTruthy();
    expect(screen.getByText("alice")).toBeTruthy();
    expect(screen.getByText("yes")).toBeTruthy();
    expect(screen.getByText("confirmed")).toBeTruthy();
    expect(screen.queryByText(/otpauth:/i)).toBeNull();
  });
});
