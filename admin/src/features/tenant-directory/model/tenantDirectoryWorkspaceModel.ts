import type {
  AdminIntegrationClientScope,
  AdminTenantDirectoryStatus,
  AdminTenantDirectoryTenantView,
} from "../../../shared/types/admin-contracts";
import { integrationClientScopeOptions } from "../../../shared/types/integration-client-scopes";

export interface TenantDirectoryWorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

export type TenantDirectoryPendingAction =
  | "load"
  | "select"
  | "quickCreate"
  | "manualCreate"
  | "copy"
  | null;

export interface TenantQuickCreateDraft {
  tenantDisplayName: string;
  applicationDisplayName: string;
  integrationClientDisplayName: string;
  allowedScopes: AdminIntegrationClientScope[];
}

export interface TenantManualCreateDraft {
  tenantId: string;
  displayName: string;
  slug: string;
  status: AdminTenantDirectoryStatus;
}

export interface TenantOneTimeSecret {
  tenantId: string;
  applicationClientId: string;
  clientId: string;
  clientSecret: string;
}

export const tenantStatusOptions: AdminTenantDirectoryStatus[] = ["active", "test", "disabled", "archived"];

export function createQuickCreateDraft(): TenantQuickCreateDraft {
  return {
    tenantDisplayName: "",
    applicationDisplayName: "",
    integrationClientDisplayName: "",
    allowedScopes: ["challenges:read", "challenges:write", "devices:write", "enrollments:write"],
  };
}

export function createManualCreateDraft(): TenantManualCreateDraft {
  return {
    tenantId: "",
    displayName: "",
    slug: "",
    status: "active",
  };
}

export function toggleScopeValue(
  scopes: AdminIntegrationClientScope[],
  scope: AdminIntegrationClientScope,
): AdminIntegrationClientScope[] {
  const nextScopes = scopes.includes(scope)
    ? scopes.filter((item) => item !== scope)
    : [...scopes, scope];

  return integrationClientScopeOptions
    .map((item) => item.value)
    .filter((item) => nextScopes.includes(item));
}

export function upsertTenant(
  tenants: AdminTenantDirectoryTenantView[],
  tenant: AdminTenantDirectoryTenantView,
): AdminTenantDirectoryTenantView[] {
  const index = tenants.findIndex((item) => item.tenantId === tenant.tenantId);
  if (index < 0) {
    return [tenant, ...tenants];
  }

  const next = [...tenants];
  next[index] = tenant;
  return next;
}
