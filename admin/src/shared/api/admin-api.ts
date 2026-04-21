import { runtimeConfig } from "../config/runtime";
import { isProblemDetails } from "../problem/problem-details";
import type {
  AdminDeliveryStatusListFilters,
  AdminDeliveryStatusView,
  AdminSession,
  AdminUserDeviceView,
  ConfirmEnrollmentRequest,
  ProblemDetails,
  StartEnrollmentRequest,
  TotpEnrollmentCommandResponse,
  TotpEnrollmentCurrent,
  UpsertWebhookSubscriptionRequest,
  WebhookSubscriptionView,
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

  async listWebhookSubscriptions(tenantId: string, applicationClientId?: string): Promise<WebhookSubscriptionView[]> {
    const query = applicationClientId?.trim()
      ? `?applicationClientId=${encodeURIComponent(applicationClientId)}`
      : "";
    return this.requestJson<WebhookSubscriptionView[]>(
      `/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/webhook-subscriptions${query}`,
    );
  }

  async upsertWebhookSubscription(request: UpsertWebhookSubscriptionRequest): Promise<WebhookSubscriptionView> {
    return this.postJson<WebhookSubscriptionView>("/api/v1/admin/webhook-subscriptions", request);
  }

  async listDeliveryStatuses(
    tenantId: string,
    filters: AdminDeliveryStatusListFilters = {},
  ): Promise<AdminDeliveryStatusView[]> {
    const queryParts: string[] = [];
    const applicationClientId = filters.applicationClientId?.trim();

    if (applicationClientId) {
      queryParts.push(`applicationClientId=${encodeURIComponent(applicationClientId)}`);
    }

    if (filters.channel) {
      queryParts.push(`channel=${encodeURIComponent(filters.channel)}`);
    }

    if (filters.status) {
      queryParts.push(`status=${encodeURIComponent(filters.status)}`);
    }

    if (typeof filters.limit === "number") {
      queryParts.push(`limit=${encodeURIComponent(filters.limit.toString())}`);
    }

    const suffix = queryParts.length > 0 ? `?${queryParts.join("&")}` : "";
    return this.requestJson<AdminDeliveryStatusView[]>(
      `/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/delivery-statuses${suffix}`,
    );
  }

  async listUserDevices(tenantId: string, externalUserId: string): Promise<AdminUserDeviceView[]> {
    return this.requestJson<AdminUserDeviceView[]>(
      `/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(externalUserId)}/devices`,
    );
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

  async revokeUserDevice(tenantId: string, externalUserId: string, deviceId: string): Promise<AdminUserDeviceView> {
    return this.postWithoutBody<AdminUserDeviceView>(
      `/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(externalUserId)}/devices/${encodeURIComponent(deviceId)}/revoke`,
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
