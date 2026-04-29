import { startTransition, useEffect, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type {
  AdminIntegrationClientScope,
  AdminIntegrationClientView,
  AdminSession,
  AdminTenantDirectoryDetailView,
} from "../../../shared/types/admin-contracts";
import {
  createClientDraft,
  haveSameScopes,
  sortScopes,
  toggleScopeValue,
  upsertClient,
  type TenantClientCreateDraft,
  type TenantManagementNotice,
  type TenantManagementPendingAction,
  type TenantManagementTab,
  type TenantOneTimeQrPayload,
  type TenantOneTimeSecret,
} from "./tenantManagementModel";
import { useTenantManagementDeviceRuntime } from "./useTenantManagementDeviceRuntime";

export function useTenantManagementWorkspace(
  session: AdminSession,
  directory: AdminTenantDirectoryDetailView,
) {
  const tenantId = directory.tenant.tenantId;
  const canReadClients = session.permissions.includes("integration-clients.read");
  const canWriteClients = session.permissions.includes("integration-clients.write");
  const [activeTab, setActiveTab] = useState<TenantManagementTab>("overview");
  const [clients, setClients] = useState<AdminIntegrationClientView[]>(directory.integrationClients);
  const [selectedClientId, setSelectedClientId] = useState<string | null>(directory.integrationClients[0]?.clientId ?? null);
  const [clientDraft, setClientDraft] = useState<TenantClientCreateDraft>(() => createClientDraft(directory));
  const [scopeDraft, setScopeDraft] = useState<AdminIntegrationClientScope[]>([]);
  const [oneTimeSecret, setOneTimeSecret] = useState<TenantOneTimeSecret | null>(null);
  const [rotatedSecret, setRotatedSecret] = useState<TenantOneTimeSecret | null>(null);
  const [rotateArmed, setRotateArmed] = useState(false);
  const [deactivateArmed, setDeactivateArmed] = useState(false);
  const [reactivateArmed, setReactivateArmed] = useState(false);
  const [notice, setNotice] = useState<TenantManagementNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<TenantManagementPendingAction>(null);
  const deviceRuntime = useTenantManagementDeviceRuntime({
    session,
    directory,
    setNotice,
    setPendingAction,
  });

  useEffect(() => {
    const selectedClient = directory.integrationClients[0] ?? null;
    setClients(directory.integrationClients);
    setSelectedClientId(selectedClient?.clientId ?? null);
    setScopeDraft(selectedClient ? sortScopes(selectedClient.allowedScopes) : []);
    setClientDraft(createClientDraft(directory));
    clearClientSecretsAndConfirmations();
    setNotice(null);
    deviceRuntime.resetForDirectory(directory);
  }, [directory]);

  const selectedClient = useMemo(
    () => clients.find((client) => client.clientId === selectedClientId) ?? null,
    [clients, selectedClientId],
  );
  const hasScopeChanges = selectedClient !== null && !haveSameScopes(scopeDraft, selectedClient.allowedScopes);

  async function loadClients() {
    if (!canReadClients) return showNotice("danger", "Недостаточно прав", "Для загрузки API clients нужен permission `integration-clients.read`.");

    setPendingAction("loadClients");
    clearClientSecretsAndConfirmations();
    try {
      const items = await adminApi.listIntegrationClients(tenantId);
      startTransition(() => {
        setClients(items);
        selectClientState(items[0] ?? null);
        setPendingAction(null);
        showNotice("success", "API clients loaded", `Tenant context: ${tenantId}. Loaded ${items.length} client(s).`);
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function createClient() {
    if (!canWriteClients) return showNotice("danger", "Недостаточно прав", "Для создания API client нужен permission `integration-clients.write`.");

    const request = {
      tenantId,
      applicationClientId: clientDraft.applicationClientId.trim(),
      clientId: clientDraft.clientId.trim(),
      allowedScopes: clientDraft.allowedScopes,
    };
    if (!request.applicationClientId || !request.clientId) return showNotice("danger", "Нужны идентификаторы", "Выберите application и задайте clientId.");
    if (request.allowedScopes.length === 0) return showNotice("danger", "Нужен scope", "Выберите хотя бы один supported integration scope.");

    setPendingAction("createClient");
    clearClientSecretsAndConfirmations();
    try {
      const response = await adminApi.createIntegrationClient(request);
      applyClient(response.client);
      setOneTimeSecret({ clientId: response.client.clientId, clientSecret: response.clientSecret });
      setClientDraft(createClientDraft(directory));
      showNotice("success", "API client created", "One-time client secret exists only in current browser state.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function rotateSecret() {
    if (!ensureSelectedClientWrite("rotate secret")) return;
    if (!rotateArmed) return showNotice("neutral", "Нужно подтверждение", "Подтвердите rotation перед заменой secret.");

    setPendingAction("rotate");
    setOneTimeSecret(null);
    setRotatedSecret(null);
    try {
      const response = await adminApi.rotateIntegrationClientSecret(tenantId, selectedClient!.clientId);
      applyClient(response.client);
      setRotatedSecret({ clientId: response.client.clientId, clientSecret: response.clientSecret });
      setRotateArmed(false);
      showNotice("success", "Secret rotated", "Rotated secret returned once and is not stored by the read model.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function updateScopes() {
    if (!ensureSelectedClientWrite("update scopes")) return;
    if (scopeDraft.length === 0) return showNotice("danger", "Нужен scope", "Оставьте хотя бы один supported scope.");
    if (!hasScopeChanges) return showNotice("neutral", "Scopes не изменились", "Выберите новый набор scopes перед сохранением.");

    setPendingAction("scopes");
    setRotatedSecret(null);
    try {
      const client = await adminApi.updateIntegrationClientScopes(tenantId, selectedClient!.clientId, { allowedScopes: scopeDraft });
      applyClient(client);
      showNotice("success", "Scopes updated", "Command used selected tenant/client context.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function deactivateClient() {
    if (!ensureSelectedClientWrite("deactivate client")) return;
    if (selectedClient!.status !== "active" || !deactivateArmed) return showNotice("neutral", "Deactivate недоступен", "Client должен быть active и подтвержден.");
    await changeClientState("deactivate", () => adminApi.deactivateIntegrationClient(tenantId, selectedClient!.clientId));
  }

  async function reactivateClient() {
    if (!ensureSelectedClientWrite("reactivate client")) return;
    if (selectedClient!.status !== "inactive" || !reactivateArmed) return showNotice("neutral", "Reactivate недоступен", "Client должен быть inactive и подтвержден.");
    await changeClientState("reactivate", () => adminApi.reactivateIntegrationClient(tenantId, selectedClient!.clientId));
  }

  async function copySecret(secret: TenantOneTimeSecret | TenantOneTimeQrPayload | null) {
    if (!secret) return;

    setPendingAction("copy");
    try {
      await navigator.clipboard.writeText("clientSecret" in secret ? secret.clientSecret : secret.qrEnvelopeValue);
      showNotice("success", "Copied", "One-time value copied. Discard it after handing it off.");
    } catch {
      showNotice("danger", "Clipboard unavailable", "Browser denied clipboard access. Select the one-time value manually.");
    } finally {
      setPendingAction(null);
    }
  }

  function selectClient(client: AdminIntegrationClientView) {
    selectClientState(client);
    clearClientSecretsAndConfirmations();
  }

  function selectClientState(client: AdminIntegrationClientView | null) {
    setSelectedClientId(client?.clientId ?? null);
    setScopeDraft(client ? sortScopes(client.allowedScopes) : []);
  }

  function applyClient(client: AdminIntegrationClientView) {
    startTransition(() => {
      setClients((current) => upsertClient(current, client));
      selectClientState(client);
      setPendingAction(null);
    });
  }

  async function changeClientState(action: "deactivate" | "reactivate", call: () => Promise<AdminIntegrationClientView>) {
    setPendingAction(action);
    setRotatedSecret(null);
    try {
      const client = await call();
      applyClient(client);
      setDeactivateArmed(false);
      setReactivateArmed(false);
      showNotice("success", action === "deactivate" ? "Client deactivated" : "Client reactivated", "Issued-token state was invalidated by backend.");
    } catch (error) {
      handleApiError(error);
    }
  }

  function clearClientSecretsAndConfirmations() {
    setOneTimeSecret(null);
    setRotatedSecret(null);
    setRotateArmed(false);
    setDeactivateArmed(false);
    setReactivateArmed(false);
    deviceRuntime.setOneTimeQrPayload(null);
  }

  function ensureSelectedClientWrite(action: string): boolean {
    if (!canWriteClients) {
      showNotice("danger", "Недостаточно прав", `Для ${action} нужен permission \`integration-clients.write\`.`);
      return false;
    }
    if (!selectedClient) {
      showNotice("danger", "Client не выбран", "Выберите API client в tenant management.");
      return false;
    }
    return true;
  }

  function showNotice(tone: TenantManagementNotice["tone"], title: string, detail: string) {
    setNotice({ tone, title, detail });
  }

  function handleApiError(error: unknown) {
    const message = mapAdminError(error);
    startTransition(() => {
      setPendingAction(null);
      setNotice({ tone: "danger", ...message });
    });
  }

  return {
    activeTab, setActiveTab, tenantId,
    clients, selectedClient, selectedClientId, selectClient,
    clientDraft, setClientDraft, scopeDraft, hasScopeChanges,
    oneTimeSecret, rotatedSecret, rotateArmed, setRotateArmed,
    deactivateArmed, setDeactivateArmed, reactivateArmed, setReactivateArmed,
    notice, pendingAction, canReadClients, canWriteClients,
    loadClients, createClient, rotateSecret, updateScopes, deactivateClient, reactivateClient,
    toggleClientCreateScope: (scope: AdminIntegrationClientScope) => setClientDraft((current) => ({ ...current, allowedScopes: toggleScopeValue(current.allowedScopes, scope) })),
    toggleLifecycleScope: (scope: AdminIntegrationClientScope) => setScopeDraft((current) => toggleScopeValue(current, scope)),
    copyOneTimeSecret: () => copySecret(oneTimeSecret),
    copyRotatedSecret: () => copySecret(rotatedSecret),
    copyQrPayload: () => copySecret(deviceRuntime.oneTimeQrPayload),
    discardSecrets: clearClientSecretsAndConfirmations,
    ...deviceRuntime,
  };
}

export type TenantManagementWorkspaceState = ReturnType<typeof useTenantManagementWorkspace>;
