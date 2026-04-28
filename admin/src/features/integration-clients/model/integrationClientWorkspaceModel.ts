import type { AdminIntegrationClientScope, AdminIntegrationClientView } from "../../../shared/types/admin-contracts";
import { integrationClientScopeOptions } from "../../../shared/types/integration-client-scopes";

export interface IntegrationClientWorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

export type IntegrationClientPendingAction =
  | "load"
  | "create"
  | "copy"
  | "rotate"
  | "scopes"
  | "deactivate"
  | "reactivate"
  | null;

export interface LookupDraft {
  tenantId: string;
}

export interface CreateDraft {
  tenantId: string;
  applicationClientId: string;
  clientId: string;
  allowedScopes: AdminIntegrationClientScope[];
}

export interface OneTimeSecret {
  clientId: string;
  clientSecret: string;
}

export function createDraft(tenantId = ""): CreateDraft {
  return {
    tenantId,
    applicationClientId: "",
    clientId: "",
    allowedScopes: ["challenges:read", "challenges:write"],
  };
}

export function sortScopes(scopes: AdminIntegrationClientScope[]): AdminIntegrationClientScope[] {
  return integrationClientScopeOptions
    .map((item) => item.value)
    .filter((item) => scopes.includes(item));
}

export function toggleScopeValue(
  scopes: AdminIntegrationClientScope[],
  scope: AdminIntegrationClientScope,
): AdminIntegrationClientScope[] {
  const nextScopes = scopes.includes(scope)
    ? scopes.filter((item) => item !== scope)
    : [...scopes, scope];

  return sortScopes(nextScopes);
}

export function haveSameScopes(
  left: AdminIntegrationClientScope[],
  right: AdminIntegrationClientScope[],
): boolean {
  const sortedLeft = sortScopes(left);
  const sortedRight = sortScopes(right);
  return sortedLeft.length === sortedRight.length && sortedLeft.every((scope, index) => scope === sortedRight[index]);
}

export function upsertClient(
  clients: AdminIntegrationClientView[],
  client: AdminIntegrationClientView,
): AdminIntegrationClientView[] {
  const index = clients.findIndex((item) => item.clientId === client.clientId);
  if (index < 0) {
    return [client, ...clients];
  }

  const next = [...clients];
  next[index] = client;
  return next;
}
