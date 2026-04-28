import type { AdminIntegrationClientScope } from "./admin-contracts";

export interface IntegrationClientScopeOption {
  value: AdminIntegrationClientScope;
  label: string;
  description: string;
}

export const integrationClientScopeOptions: IntegrationClientScopeOption[] = [
  {
    value: "challenges:read",
    label: "challenges:read",
    description: "Read challenge state for integration status checks.",
  },
  {
    value: "challenges:write",
    label: "challenges:write",
    description: "Create challenge requests and drive MFA prompts.",
  },
  {
    value: "enrollments:write",
    label: "enrollments:write",
    description: "Start trusted enrollment flows outside operator UI.",
  },
  {
    value: "devices:write",
    label: "devices:write",
    description: "Use device activation and device lifecycle commands.",
  },
];
