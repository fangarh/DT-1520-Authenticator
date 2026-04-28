import { startTransition, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type { AdminIntegrationClientScope, AdminIntegrationClientView, AdminSession } from "../../../shared/types/admin-contracts";
import {
  createDraft,
  haveSameScopes,
  sortScopes,
  toggleScopeValue,
  upsertClient,
  type CreateDraft,
  type IntegrationClientPendingAction,
  type IntegrationClientWorkspaceNotice,
  type LookupDraft,
  type OneTimeSecret,
} from "./integrationClientWorkspaceModel";

export function useIntegrationClientWorkspace(session: AdminSession) {
  const canRead = session.permissions.includes("integration-clients.read");
  const canWrite = session.permissions.includes("integration-clients.write");
  const [lookupDraft, setLookupDraft] = useState<LookupDraft>({ tenantId: "" });
  const [createDraftState, setCreateDraft] = useState<CreateDraft>(createDraft());
  const [clients, setClients] = useState<AdminIntegrationClientView[]>([]);
  const [selectedClientId, setSelectedClientId] = useState<string | null>(null);
  const [scopeDraft, setScopeDraft] = useState<AdminIntegrationClientScope[]>([]);
  const [oneTimeSecret, setOneTimeSecret] = useState<OneTimeSecret | null>(null);
  const [rotatedSecret, setRotatedSecret] = useState<OneTimeSecret | null>(null);
  const [rotateArmed, setRotateArmed] = useState(false);
  const [deactivateArmed, setDeactivateArmed] = useState(false);
  const [reactivateArmed, setReactivateArmed] = useState(false);
  const [notice, setNotice] = useState<IntegrationClientWorkspaceNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<IntegrationClientPendingAction>(null);

  const selectedClient = useMemo(
    () => clients.find((client) => client.clientId === selectedClientId) ?? null,
    [clients, selectedClientId],
  );
  const hasScopeChanges = selectedClient !== null && !haveSameScopes(scopeDraft, selectedClient.allowedScopes);

  async function loadClients() {
    if (!canRead) return showNotice("danger", "Недостаточно прав", "Для загрузки integration clients нужен permission `integration-clients.read`.");

    const tenantId = lookupDraft.tenantId.trim();
    if (!tenantId) return showNotice("danger", "Нужен tenantId", "Укажите tenantId перед загрузкой integration clients.");

    setPendingAction("load");
    clearSecretsAndConfirmations();
    try {
      const items = await adminApi.listIntegrationClients(tenantId);
      startTransition(() => {
        const nextSelected = items.some((item) => item.clientId === selectedClientId)
          ? selectedClientId
          : items[0]?.clientId ?? null;
        const nextClient = items.find((item) => item.clientId === nextSelected) ?? null;

        setClients(items);
        setSelectedClientId(nextSelected);
        setScopeDraft(nextClient ? sortScopes(nextClient.allowedScopes) : []);
        setCreateDraft((current) => ({ ...current, tenantId }));
        setPendingAction(null);
        showNotice(
          "success",
          "Integration clients loaded",
          items.length === 0
            ? "Для выбранного tenant пока нет integration clients."
            : `Загружено ${items.length} client(s) для operator review.`,
        );
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function createClient() {
    if (!canWrite) return showNotice("danger", "Недостаточно прав", "Для создания integration client нужен permission `integration-clients.write`.");

    const request = {
      tenantId: createDraftState.tenantId.trim(),
      applicationClientId: createDraftState.applicationClientId.trim(),
      clientId: createDraftState.clientId.trim(),
      allowedScopes: createDraftState.allowedScopes,
    };

    if (!request.tenantId || !request.applicationClientId || !request.clientId) return showNotice("danger", "Нужны идентификаторы", "Заполните tenantId, applicationClientId и clientId перед созданием.");

    if (request.allowedScopes.length === 0) return showNotice("danger", "Нужен scope", "Выберите хотя бы один supported integration scope.");

    setPendingAction("create");
    setOneTimeSecret(null);
    setRotatedSecret(null);
    try {
      const response = await adminApi.createIntegrationClient(request);
      applyClient(response.client);
      setLookupDraft({ tenantId: response.client.tenantId });
      setCreateDraft(createDraft(response.client.tenantId));
      setOneTimeSecret({ clientId: response.client.clientId, clientSecret: response.clientSecret });
      showNotice("success", "Integration client created", "Server generated a one-time client secret. Copy it now or discard it from this browser state.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function rotateSecret() {
    if (!ensureSelectedWriteClient("rotate secret")) return;

    if (!rotateArmed) return showNotice("neutral", "Нужно подтверждение", "Подтвердите rotation перед заменой client secret.");

    setPendingAction("rotate");
    setOneTimeSecret(null);
    setRotatedSecret(null);
    try {
      const response = await adminApi.rotateIntegrationClientSecret(selectedClient!.tenantId, selectedClient!.clientId);
      applyClient(response.client);
      setRotatedSecret({ clientId: response.client.clientId, clientSecret: response.clientSecret });
      setRotateArmed(false);
      showNotice("success", "Secret rotated", "Backend returned a one-time rotated secret. Copy it before leaving this browser state.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function updateScopes() {
    if (!ensureSelectedWriteClient("update scopes")) return;

    if (scopeDraft.length === 0) return showNotice("danger", "Нужен scope", "Выберите хотя бы один supported integration scope перед сохранением.");

    if (!hasScopeChanges) return showNotice("neutral", "Scopes не изменились", "Выберите новый набор scopes перед сохранением.");

    setPendingAction("scopes");
    setRotatedSecret(null);
    try {
      const client = await adminApi.updateIntegrationClientScopes(selectedClient!.tenantId, selectedClient!.clientId, {
        allowedScopes: scopeDraft,
      });
      applyClient(client);
      showNotice("success", "Scopes updated", "Allowed scopes сохранены, а runtime auth state обновлен backend-ом.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function deactivateClient() {
    if (!ensureSelectedWriteClient("deactivate client")) return;

    if (selectedClient!.status !== "active" || !deactivateArmed) return showNotice("neutral", "Deactivate недоступен", "Client должен быть active, а destructive action должен быть подтвержден.");

    await changeActiveState("deactivate", () => adminApi.deactivateIntegrationClient(selectedClient!.tenantId, selectedClient!.clientId));
  }

  async function reactivateClient() {
    if (!ensureSelectedWriteClient("reactivate client")) return;

    if (selectedClient!.status !== "inactive" || !reactivateArmed) return showNotice("neutral", "Reactivate недоступен", "Client должен быть inactive, а lifecycle action должен быть подтвержден.");

    await changeActiveState("reactivate", () => adminApi.reactivateIntegrationClient(selectedClient!.tenantId, selectedClient!.clientId));
  }

  async function copySecret(secret: OneTimeSecret | null) {
    if (!secret) return;

    setPendingAction("copy");
    try {
      await navigator.clipboard.writeText(secret.clientSecret);
      showNotice("success", "Secret copied", "One-time secret copied to clipboard. It remains visible only until discard or reload.");
    } catch {
      showNotice("danger", "Clipboard unavailable", "Browser did not allow clipboard access. Select and copy the secret manually, then discard it.");
    } finally {
      setPendingAction(null);
    }
  }

  function discardSecret(kind: "create" | "rotate") {
    if (kind === "create") setOneTimeSecret(null);
    else setRotatedSecret(null);

    showNotice("neutral", "Secret discarded", "One-time client secret removed from current UI state. It cannot be read back from backend.");
  }

  function selectClient(client: AdminIntegrationClientView) {
    setSelectedClientId(client.clientId);
    setScopeDraft(sortScopes(client.allowedScopes));
    clearSecretsAndConfirmations();
  }

  function toggleCreateScope(scope: AdminIntegrationClientScope) {
    setCreateDraft((current) => ({ ...current, allowedScopes: toggleScopeValue(current.allowedScopes, scope) }));
  }

  function toggleLifecycleScope(scope: AdminIntegrationClientScope) {
    setScopeDraft((current) => toggleScopeValue(current, scope));
  }

  function resetCreateDraft() {
    setCreateDraft(createDraft(lookupDraft.tenantId.trim()));
    setOneTimeSecret(null);
    showNotice("neutral", "Create form reset", "Create form cleared without storing any generated secret.");
  }

  async function changeActiveState(
    action: Exclude<IntegrationClientPendingAction, "load" | "create" | "copy" | "rotate" | "scopes" | null>,
    call: () => Promise<AdminIntegrationClientView>,
  ) {
    setPendingAction(action);
    setRotatedSecret(null);
    try {
      const client = await call();
      applyClient(client);
      setDeactivateArmed(false);
      setReactivateArmed(false);
      showNotice(
        "success",
        action === "deactivate" ? "Client deactivated" : "Client reactivated",
        action === "deactivate"
          ? "Integration client переведен в inactive, а issued tokens invalidated backend-ом."
          : "Integration client снова active, а runtime auth state обновлен backend-ом.",
      );
    } catch (error) {
      handleApiError(error);
    }
  }

  function applyClient(client: AdminIntegrationClientView) {
    startTransition(() => {
      setClients((current) => upsertClient(current, client));
      setSelectedClientId(client.clientId);
      setScopeDraft(sortScopes(client.allowedScopes));
      setPendingAction(null);
    });
  }

  function clearSecretsAndConfirmations() {
    setOneTimeSecret(null);
    setRotatedSecret(null);
    setRotateArmed(false);
    setDeactivateArmed(false);
    setReactivateArmed(false);
  }

  function ensureSelectedWriteClient(action: string): boolean {
    if (!canWrite) {
      showNotice("danger", "Недостаточно прав", `Для ${action} нужен permission \`integration-clients.write\`.`);
      return false;
    }

    if (!selectedClient) {
      showNotice("danger", "Client не выбран", "Выберите integration client из списка перед lifecycle action.");
      return false;
    }

    return true;
  }

  function showNotice(tone: IntegrationClientWorkspaceNotice["tone"], title: string, detail: string) {
    setNotice({ tone, title, detail });
  }

  function handleApiError(error: unknown) {
    const message = mapAdminError(error);
    startTransition(() => {
      setPendingAction(null);
      setRotateArmed(false);
      setDeactivateArmed(false);
      setReactivateArmed(false);
      setNotice({ tone: "danger", ...message });
    });
  }

  return {
    lookupDraft, setLookupDraft,
    createDraft: createDraftState, setCreateDraft,
    clients, selectedClient, selectedClientId,
    scopeDraft, hasScopeChanges,
    oneTimeSecret, rotatedSecret,
    rotateArmed, setRotateArmed,
    deactivateArmed, setDeactivateArmed,
    reactivateArmed, setReactivateArmed,
    notice, pendingAction, canRead, canWrite,
    loadClients, createClient, rotateSecret, updateScopes, deactivateClient, reactivateClient,
    copyCreateSecret: () => copySecret(oneTimeSecret), copyRotatedSecret: () => copySecret(rotatedSecret),
    discardCreateSecret: () => discardSecret("create"), discardRotatedSecret: () => discardSecret("rotate"),
    selectClient, toggleCreateScope, toggleLifecycleScope, resetCreateDraft,
  };
}
