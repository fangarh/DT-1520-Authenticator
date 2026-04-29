import { startTransition, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type { AdminDeviceOnboardingArtifactView, AdminSession } from "../../../shared/types/admin-contracts";
import {
  createArtifactDraft,
  createLookupDraft,
  parsePositiveInteger,
  upsertArtifact,
  type CreateDraft,
  type DeviceOnboardingPendingAction,
  type DeviceOnboardingWorkspaceNotice,
  type LookupDraft,
  type OneTimeActivationPayload,
} from "./deviceOnboardingWorkspaceModel";
import { resolveDeviceOnboardingRuntimeBaseUrl } from "./deviceOnboardingQrEnvelope";

export function useDeviceOnboardingWorkspace(session: AdminSession) {
  const canRead = session.permissions.includes("devices.read");
  const canWrite = session.permissions.includes("devices.write");
  const [lookupDraft, setLookupDraft] = useState<LookupDraft>(createLookupDraft);
  const [createDraft, setCreateDraft] = useState<CreateDraft>(createArtifactDraft);
  const [artifacts, setArtifacts] = useState<AdminDeviceOnboardingArtifactView[]>([]);
  const [selectedArtifactId, setSelectedArtifactId] = useState<string | null>(null);
  const [oneTimePayload, setOneTimePayload] = useState<OneTimeActivationPayload | null>(null);
  const [revokeArmed, setRevokeArmed] = useState(false);
  const [notice, setNotice] = useState<DeviceOnboardingWorkspaceNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<DeviceOnboardingPendingAction>(null);

  const selectedArtifact = useMemo(
    () => artifacts.find((artifact) => artifact.activationCodeId === selectedArtifactId) ?? null,
    [artifacts, selectedArtifactId],
  );

  async function loadArtifacts() {
    if (!canRead) return showNotice("danger", "Недостаточно прав", "Для просмотра onboarding artifacts нужен permission `devices.read`.");

    const tenantId = lookupDraft.tenantId.trim();
    if (!tenantId) return showNotice("danger", "Нужен tenantId", "Укажите tenantId перед загрузкой QR onboarding artifacts.");

    const limit = parsePositiveInteger(lookupDraft.limit);
    if (!limit) return showNotice("danger", "Неверный limit", "Limit должен быть положительным целым числом.");

    setPendingAction("load");
    clearSecretState();
    try {
      const items = await adminApi.listDeviceOnboardingArtifacts(tenantId, {
        externalUserId: lookupDraft.externalUserId,
        applicationClientId: lookupDraft.applicationClientId,
        status: lookupDraft.status || undefined,
        limit,
      });
      startTransition(() => {
        setArtifacts(items);
        setSelectedArtifactId((current) => items.some((item) => item.activationCodeId === current) ? current : items[0]?.activationCodeId ?? null);
        setCreateDraft((current) => ({ ...current, tenantId }));
        setPendingAction(null);
        showNotice(
          "success",
          "Onboarding artifacts loaded",
          items.length === 0
            ? "Для выбранного фильтра нет device onboarding artifacts."
            : `Загружено ${items.length} artifact(s) без раскрытия activation payload.`,
        );
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function createArtifact() {
    if (!canWrite) return showNotice("danger", "Недостаточно прав", "Для выпуска QR artifact нужен permission `devices.write`.");

    const request = {
      tenantId: createDraft.tenantId.trim(),
      applicationClientId: createDraft.applicationClientId.trim(),
      externalUserId: createDraft.externalUserId.trim(),
      platform: createDraft.platform,
      ttlMinutes: parsePositiveInteger(createDraft.ttlMinutes) ?? 0,
    };

    if (!request.tenantId || !request.applicationClientId || !request.externalUserId) {
      return showNotice("danger", "Нужны идентификаторы", "Заполните tenantId, applicationClientId и externalUserId перед выпуском QR.");
    }

    if (request.ttlMinutes <= 0) {
      return showNotice("danger", "Неверный TTL", "TTL должен быть положительным числом минут.");
    }

    setPendingAction("create");
    clearSecretState();
    try {
      const response = await adminApi.createDeviceOnboardingArtifact(request);
      startTransition(() => {
        setArtifacts((current) => upsertArtifact(current, response.artifact));
        setSelectedArtifactId(response.artifact.activationCodeId);
        setLookupDraft((current) => ({
          ...current,
          tenantId: response.artifact.tenantId,
          externalUserId: response.artifact.externalUserId,
          applicationClientId: response.artifact.applicationClientId,
          status: "pending",
        }));
        setOneTimePayload({
          activationCodeId: response.artifact.activationCodeId,
          activationPayload: response.activationPayload,
          runtimeBaseUrl: resolveDeviceOnboardingRuntimeBaseUrl(),
          expiresAtUtc: response.artifact.expiresAtUtc,
        });
        setPendingAction(null);
        showNotice("success", "QR artifact created", "Activation payload is visible only in current browser state. It cannot be read back from list.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function revokeArtifact() {
    if (!canWrite) return showNotice("danger", "Недостаточно прав", "Для revoke QR artifact нужен permission `devices.write`.");
    if (!selectedArtifact) return showNotice("danger", "Artifact не выбран", "Выберите pending artifact перед revoke.");
    if (selectedArtifact.status !== "pending" || !revokeArmed) {
      return showNotice("neutral", "Revoke недоступен", "Artifact должен быть pending, а revoke должен быть подтвержден.");
    }

    setPendingAction("revoke");
    setOneTimePayload(null);
    try {
      const revoked = await adminApi.revokeDeviceOnboardingArtifact(selectedArtifact.tenantId, selectedArtifact.activationCodeId);
      startTransition(() => {
        setArtifacts((current) => upsertArtifact(current, revoked));
        setSelectedArtifactId(revoked.activationCodeId);
        setRevokeArmed(false);
        setPendingAction(null);
        showNotice("success", "QR artifact revoked", "Artifact больше не может быть использован для activation.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function copyActivationPayload() {
    if (!oneTimePayload) return;

    setPendingAction("copy");
    try {
      await navigator.clipboard.writeText(oneTimePayload.activationPayload);
      showNotice("success", "Payload copied", "One-time activation payload copied. It remains visible only until discard, reload or another command.");
    } catch {
      showNotice("danger", "Clipboard unavailable", "Browser did not allow clipboard access. Select and copy the payload manually, then discard it.");
    } finally {
      setPendingAction(null);
    }
  }

  function selectArtifact(artifact: AdminDeviceOnboardingArtifactView) {
    setSelectedArtifactId(artifact.activationCodeId);
    clearSecretState();
  }

  function discardActivationPayload() {
    setOneTimePayload(null);
    showNotice("neutral", "Payload discarded", "Activation payload removed from current UI state. Backend list cannot return it again.");
  }

  function resetCreateDraft() {
    setCreateDraft(createArtifactDraft(lookupDraft.tenantId.trim()));
    clearSecretState();
    showNotice("neutral", "Create form reset", "QR create draft cleared without storing activation payload.");
  }

  function clearSecretState() {
    setOneTimePayload(null);
    setRevokeArmed(false);
  }

  function showNotice(tone: DeviceOnboardingWorkspaceNotice["tone"], title: string, detail: string) {
    setNotice({ tone, title, detail });
  }

  function handleApiError(error: unknown) {
    const message = mapAdminError(error);
    startTransition(() => {
      setPendingAction(null);
      setRevokeArmed(false);
      setNotice({ tone: "danger", ...message });
    });
  }

  return {
    lookupDraft, setLookupDraft,
    createDraft, setCreateDraft,
    artifacts, selectedArtifact, selectedArtifactId,
    oneTimePayload,
    revokeArmed, setRevokeArmed,
    notice, pendingAction, canRead, canWrite,
    loadArtifacts, createArtifact, revokeArtifact,
    copyActivationPayload, discardActivationPayload, resetCreateDraft, selectArtifact,
  };
}
