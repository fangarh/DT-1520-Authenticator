import { startTransition, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type { AdminSession, AdminUserDeviceView } from "../../../shared/types/admin-contracts";

export interface UserDeviceWorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

type PendingAction = "load" | "revoke" | null;

interface LookupDraft {
  tenantId: string;
  externalUserId: string;
}

interface LoadedScope {
  tenantId: string;
  externalUserId: string;
}

const defaultLookupDraft: LookupDraft = {
  tenantId: "",
  externalUserId: "",
};

export function useUserDeviceWorkspace(session: AdminSession) {
  const canRead = session.permissions.includes("devices.read");
  const canWrite = session.permissions.includes("devices.write");
  const [lookupDraft, setLookupDraft] = useState<LookupDraft>(defaultLookupDraft);
  const [loadedScope, setLoadedScope] = useState<LoadedScope | null>(null);
  const [devices, setDevices] = useState<AdminUserDeviceView[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [revokeArmed, setRevokeArmed] = useState(false);
  const [notice, setNotice] = useState<UserDeviceWorkspaceNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const selectedDevice = useMemo(
    () => devices.find((device) => device.deviceId === selectedDeviceId) ?? null,
    [devices, selectedDeviceId],
  );
  const hasDraftScopeChanges = loadedScope !== null && (
    loadedScope.tenantId !== lookupDraft.tenantId.trim() ||
    loadedScope.externalUserId !== lookupDraft.externalUserId.trim()
  );

  async function loadDevices() {
    if (!canRead) {
      setNotice({
        tone: "danger",
        title: "Недостаточно прав",
        detail: "Для просмотра устройств нужен permission `devices.read`.",
      });
      return;
    }

    const tenantId = lookupDraft.tenantId.trim();
    const externalUserId = lookupDraft.externalUserId.trim();

    if (!tenantId) {
      setNotice({
        tone: "danger",
        title: "Нужен tenantId",
        detail: "Укажите tenantId перед загрузкой user device history.",
      });
      return;
    }

    if (!externalUserId) {
      setNotice({
        tone: "danger",
        title: "Нужен externalUserId",
        detail: "Укажите внешний идентификатор пользователя перед загрузкой устройств.",
      });
      return;
    }

    setPendingAction("load");
    try {
      const items = await adminApi.listUserDevices(tenantId, externalUserId);
      startTransition(() => {
        setLoadedScope({ tenantId, externalUserId });
        setDevices(items);
        setSelectedDeviceId((current) => items.some((device) => device.deviceId === current) ? current : items[0]?.deviceId ?? null);
        setRevokeArmed(false);
        setPendingAction(null);
        setNotice({
          tone: "success",
          title: "Devices loaded",
          detail: items.length === 0
            ? "Для выбранного пользователя device history пока не найдена."
            : `Загружено ${items.length} current/recent device record(s) для operator review.`,
        });
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function revokeDevice() {
    if (!canWrite) {
      setNotice({
        tone: "danger",
        title: "Недостаточно прав",
        detail: "Для revoke устройства нужен permission `devices.write`.",
      });
      return;
    }

    if (!loadedScope) {
      setNotice({
        tone: "danger",
        title: "Сначала загрузите user scope",
        detail: "Revoke action доступен только после успешной загрузки devices для tenant и external user.",
      });
      return;
    }

    if (!selectedDevice) {
      setNotice({
        tone: "danger",
        title: "Устройство не выбрано",
        detail: "Выберите устройство из списка перед revoke.",
      });
      return;
    }

    if (selectedDevice.status !== "active") {
      setNotice({
        tone: "neutral",
        title: "Revoke недоступен",
        detail: "Только устройства в состоянии `active` можно revoke-ить из этого workspace.",
      });
      return;
    }

    if (!revokeArmed) {
      setNotice({
        tone: "neutral",
        title: "Нужно подтверждение",
        detail: "Подтвердите destructive action перед revoke устройства.",
      });
      return;
    }

    setPendingAction("revoke");
    try {
      const revokedDevice = await adminApi.revokeUserDevice(
        loadedScope.tenantId,
        loadedScope.externalUserId,
        selectedDevice.deviceId,
      );

      startTransition(() => {
        setDevices((current) => current.map((device) => (
          device.deviceId === revokedDevice.deviceId ? revokedDevice : device
        )));
        setSelectedDeviceId(revokedDevice.deviceId);
        setRevokeArmed(false);
        setPendingAction(null);
        setNotice({
          tone: "success",
          title: "Device revoked",
          detail: "Выбранное устройство переведено в `revoked`, а текущая device session больше не может использоваться.",
        });
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  function selectDevice(device: AdminUserDeviceView) {
    setSelectedDeviceId(device.deviceId);
    setRevokeArmed(false);
  }

  function handleApiError(error: unknown) {
    const message = mapAdminError(error);
    startTransition(() => {
      setPendingAction(null);
      setRevokeArmed(false);
      setNotice({
        tone: "danger",
        ...message,
      });
    });
  }

  return {
    lookupDraft,
    setLookupDraft,
    loadedScope,
    hasDraftScopeChanges,
    devices,
    selectedDeviceId,
    selectedDevice,
    revokeArmed,
    setRevokeArmed,
    notice,
    pendingAction,
    canRead,
    canWrite,
    loadDevices,
    revokeDevice,
    selectDevice,
  };
}
