import type { InstallerRunRequest, InstallerRunResponse, InstallerShellInfo } from "../types/installer-contracts";

class InstallerApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "InstallerApiError";
    this.status = status;
  }
}

async function requestJson<TResponse>(input: RequestInfo, init?: RequestInit): Promise<TResponse> {
  const response = await fetch(input, init);
  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const detail = payload?.detail ?? payload?.title ?? "Unknown installer bridge failure.";
    throw new InstallerApiError(detail, response.status);
  }

  return payload as TResponse;
}

export const installerApi = {
  getShellInfo() {
    return requestJson<InstallerShellInfo>("/api/shell-info");
  },
  run(request: InstallerRunRequest) {
    return requestJson<InstallerRunResponse>("/api/run", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    });
  },
};

export { InstallerApiError };
