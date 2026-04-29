import { startTransition, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type {
  AdminIntegrationClientScope,
  AdminSession,
  AdminTenantDirectoryDetailView,
  AdminTenantDirectoryTenantView,
} from "../../../shared/types/admin-contracts";
import {
  createManualCreateDraft,
  createQuickCreateDraft,
  toggleScopeValue,
  upsertTenant,
  type TenantDirectoryPendingAction,
  type TenantDirectoryWorkspaceNotice,
  type TenantManualCreateDraft,
  type TenantOneTimeSecret,
  type TenantQuickCreateDraft,
} from "./tenantDirectoryWorkspaceModel";

export function useTenantDirectoryWorkspace(session: AdminSession) {
  const canRead = session.permissions.includes("tenants.read");
  const canWrite = session.permissions.includes("tenants.write");
  const [tenants, setTenants] = useState<AdminTenantDirectoryTenantView[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);
  const [directory, setDirectory] = useState<AdminTenantDirectoryDetailView | null>(null);
  const [quickCreateDraft, setQuickCreateDraft] = useState<TenantQuickCreateDraft>(createQuickCreateDraft);
  const [manualCreateDraft, setManualCreateDraft] = useState<TenantManualCreateDraft>(createManualCreateDraft);
  const [oneTimeSecret, setOneTimeSecret] = useState<TenantOneTimeSecret | null>(null);
  const [notice, setNotice] = useState<TenantDirectoryWorkspaceNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<TenantDirectoryPendingAction>(null);

  const selectedTenant = useMemo(
    () => tenants.find((tenant) => tenant.tenantId === selectedTenantId) ?? directory?.tenant ?? null,
    [directory, selectedTenantId, tenants],
  );

  async function loadTenants() {
    if (!canRead) return showNotice("danger", "Недостаточно прав", "Для загрузки tenants нужен permission `tenants.read`.");

    setPendingAction("load");
    clearSecret();
    try {
      const items = await adminApi.listTenants();
      startTransition(() => {
        setTenants(items);
        setSelectedTenantId(null);
        setDirectory(null);
        setPendingAction(null);
        showNotice(
          "success",
          "Tenants loaded",
          items.length === 0
            ? "Tenant directory пока пуст. Используйте quick create для первого tenant."
            : `Загружено ${items.length} tenant(s) для operator review.`,
        );
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function selectTenant(tenant: AdminTenantDirectoryTenantView) {
    if (!canRead) return showNotice("danger", "Недостаточно прав", "Для tenant detail нужен permission `tenants.read`.");

    setSelectedTenantId(tenant.tenantId);
    setPendingAction("select");
    clearSecret();
    try {
      const detail = await adminApi.getTenantDirectory(tenant.tenantId);
      startTransition(() => {
        setDirectory(detail);
        setTenants((current) => upsertTenant(current, detail.tenant));
        setPendingAction(null);
        showNotice("success", "Tenant selected", "Directory loaded without client secrets or secret hashes.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function quickCreateTenant() {
    if (!canWrite) return showNotice("danger", "Недостаточно прав", "Для quick create нужен permission `tenants.write`.");

    const request = {
      tenantDisplayName: quickCreateDraft.tenantDisplayName.trim(),
      applicationDisplayName: quickCreateDraft.applicationDisplayName.trim(),
      integrationClientDisplayName: quickCreateDraft.integrationClientDisplayName.trim(),
      allowedScopes: quickCreateDraft.allowedScopes,
    };

    if (!request.tenantDisplayName || !request.applicationDisplayName || !request.integrationClientDisplayName) {
      return showNotice("danger", "Нужны имена", "Заполните tenant, application и API client display names.");
    }

    if (request.allowedScopes.length === 0) {
      return showNotice("danger", "Нужен scope", "Выберите хотя бы один integration scope для initial API client.");
    }

    setPendingAction("quickCreate");
    clearSecret();
    try {
      const response = await adminApi.quickCreateTenant(request);
      startTransition(() => {
        setTenants((current) => upsertTenant(current, response.directory.tenant));
        setSelectedTenantId(response.directory.tenant.tenantId);
        setDirectory(response.directory);
        setOneTimeSecret({
          tenantId: response.directory.tenant.tenantId,
          applicationClientId: response.client.applicationClientId,
          clientId: response.client.clientId,
          clientSecret: response.clientSecret,
        });
        setQuickCreateDraft(createQuickCreateDraft());
        setPendingAction(null);
        showNotice("success", "Tenant quick-created", "Server generated IDs and a one-time client secret. Copy it before reload or discard.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function createTenant() {
    if (!canWrite) return showNotice("danger", "Недостаточно прав", "Для manual create нужен permission `tenants.write`.");

    const request = {
      tenantId: manualCreateDraft.tenantId.trim() || undefined,
      displayName: manualCreateDraft.displayName.trim(),
      slug: manualCreateDraft.slug.trim() || undefined,
      status: manualCreateDraft.status,
    };

    if (!request.displayName) {
      return showNotice("danger", "Нужно имя", "Укажите tenant display name перед manual create.");
    }

    setPendingAction("manualCreate");
    clearSecret();
    try {
      const tenant = await adminApi.createTenant(request);
      startTransition(() => {
        setTenants((current) => upsertTenant(current, tenant));
        setSelectedTenantId(tenant.tenantId);
        setDirectory({ tenant, applications: [], integrationClients: [] });
        setManualCreateDraft(createManualCreateDraft());
        setPendingAction(null);
        showNotice("success", "Tenant created", "Manual tenant created without application, client secret or secret material.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function copySecret() {
    if (!oneTimeSecret) return;

    setPendingAction("copy");
    try {
      await navigator.clipboard.writeText(oneTimeSecret.clientSecret);
      showNotice("success", "Secret copied", "One-time client secret copied. It remains only in current browser state.");
    } catch {
      showNotice("danger", "Clipboard unavailable", "Browser did not allow clipboard access. Select and copy the secret manually, then discard it.");
    } finally {
      setPendingAction(null);
    }
  }

  function discardSecret() {
    clearSecret();
    showNotice("neutral", "Secret discarded", "One-time client secret removed from current UI state. Backend cannot return it again.");
  }

  function toggleQuickCreateScope(scope: AdminIntegrationClientScope) {
    setQuickCreateDraft((current) => ({
      ...current,
      allowedScopes: toggleScopeValue(current.allowedScopes, scope),
    }));
  }

  function resetQuickCreateDraft() {
    setQuickCreateDraft(createQuickCreateDraft());
    clearSecret();
    showNotice("neutral", "Quick create reset", "Quick create form cleared without storing generated values.");
  }

  function resetManualCreateDraft() {
    setManualCreateDraft(createManualCreateDraft());
    showNotice("neutral", "Manual create reset", "Manual create form cleared.");
  }

  function clearSecret() {
    setOneTimeSecret(null);
  }

  function showNotice(tone: TenantDirectoryWorkspaceNotice["tone"], title: string, detail: string) {
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
    tenants, selectedTenant, selectedTenantId, directory,
    quickCreateDraft, setQuickCreateDraft,
    manualCreateDraft, setManualCreateDraft,
    oneTimeSecret, notice, pendingAction, canRead, canWrite,
    loadTenants, selectTenant, quickCreateTenant, createTenant,
    copySecret, discardSecret, toggleQuickCreateScope,
    resetQuickCreateDraft, resetManualCreateDraft,
  };
}
