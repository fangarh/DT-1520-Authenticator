import { runtimeConfig } from "../config/runtime";
import { isProblemDetails } from "../problem/problem-details";
import type {
  AdminSession,
  ConfirmEnrollmentRequest,
  ProblemDetails,
  StartEnrollmentRequest,
  TotpEnrollmentCommandResponse,
  TotpEnrollmentCurrent,
} from "../types/admin-contracts";

export class AdminApiError extends Error {
  status: number;
  problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null, fallbackMessage: string) {
    super(problem?.detail ?? problem?.title ?? fallbackMessage);
    this.name = "AdminApiError";
    this.status = status;
    this.problem = problem;
  }
}

class AdminApiClient {
  private readonly baseUrl = runtimeConfig.apiBaseUrl.replace(/\/$/, "");

  async getSession(): Promise<AdminSession> {
    return this.requestJson<AdminSession>("/api/v1/admin/auth/session");
  }

  async login(username: string, password: string): Promise<AdminSession> {
    return this.postJson<AdminSession>("/api/v1/admin/auth/login", { username, password });
  }

  async logout(): Promise<void> {
    await this.postWithoutBody("/api/v1/admin/auth/logout");
  }

  async getCurrentEnrollment(tenantId: string, externalUserId: string): Promise<TotpEnrollmentCurrent> {
    return this.requestJson<TotpEnrollmentCurrent>(
      `/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(externalUserId)}/enrollments/totp/current`,
    );
  }

  async startEnrollment(request: StartEnrollmentRequest): Promise<TotpEnrollmentCommandResponse> {
    return this.postJson<TotpEnrollmentCommandResponse>("/api/v1/admin/enrollments/totp", request);
  }

  async confirmEnrollment(enrollmentId: string, request: ConfirmEnrollmentRequest): Promise<TotpEnrollmentCommandResponse> {
    return this.postJson<TotpEnrollmentCommandResponse>(
      `/api/v1/admin/enrollments/totp/${encodeURIComponent(enrollmentId)}/confirm`,
      request,
    );
  }

  async replaceEnrollment(enrollmentId: string): Promise<TotpEnrollmentCommandResponse> {
    return this.postWithoutBody<TotpEnrollmentCommandResponse>(
      `/api/v1/admin/enrollments/totp/${encodeURIComponent(enrollmentId)}/replace`,
    );
  }

  async revokeEnrollment(enrollmentId: string): Promise<TotpEnrollmentCommandResponse> {
    return this.postWithoutBody<TotpEnrollmentCommandResponse>(
      `/api/v1/admin/enrollments/totp/${encodeURIComponent(enrollmentId)}/revoke`,
    );
  }

  private async ensureCsrfToken(): Promise<string> {
    const response = await fetch(this.toUrl("/api/v1/admin/auth/csrf-token"), {
      credentials: "include",
    });
    return this.handleResponse<{ requestToken: string }>(response, "Не удалось получить CSRF token.").then((body) => body.requestToken);
  }

  private async postJson<T>(path: string, body: object): Promise<T> {
    const csrfToken = await this.ensureCsrfToken();
    const response = await fetch(this.toUrl(path), {
      method: "POST",
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken,
      },
      body: JSON.stringify(body),
    });

    return this.handleResponse<T>(response, "POST request failed.");
  }

  private async postWithoutBody<T>(path: string): Promise<T> {
    const csrfToken = await this.ensureCsrfToken();
    const response = await fetch(this.toUrl(path), {
      method: "POST",
      credentials: "include",
      headers: {
        "X-CSRF-TOKEN": csrfToken,
      },
    });

    return this.handleResponse<T>(response, "POST request failed.");
  }

  private async requestJson<T>(path: string): Promise<T> {
    const response = await fetch(this.toUrl(path), {
      credentials: "include",
    });

    return this.handleResponse<T>(response, "GET request failed.");
  }

  private async handleResponse<T>(response: Response, fallbackMessage: string): Promise<T> {
    if (response.status === 204) {
      return undefined as T;
    }

    const text = await response.text();
    const body = text ? this.tryParseJson(text) : null;
    if (!response.ok) {
      throw new AdminApiError(
        response.status,
        isProblemDetails(body) ? body : null,
        fallbackMessage,
      );
    }

    return body as T;
  }

  private tryParseJson(text: string): unknown {
    try {
      return JSON.parse(text);
    } catch {
      return null;
    }
  }

  private toUrl(path: string): string {
    return `${this.baseUrl}${path}`;
  }
}

export const adminApi = new AdminApiClient();
