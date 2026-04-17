import type { ProblemDetails } from "../types/admin-contracts";

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === "object" && value !== null;
}
