import { startTransition, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type {
  AdminDeliveryChannel,
  AdminDeliveryStatus,
  AdminDeliveryStatusView,
  AdminSession,
} from "../../../shared/types/admin-contracts";

export interface DeliveryStatusWorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

type PendingAction = "load" | null;

interface LookupDraft {
  tenantId: string;
  applicationClientId: string;
  channel: "" | AdminDeliveryChannel;
  status: "" | AdminDeliveryStatus;
  limit: string;
}

const defaultLookupDraft: LookupDraft = {
  tenantId: "",
  applicationClientId: "",
  channel: "",
  status: "",
  limit: "25",
};

export function useDeliveryStatusWorkspace(session: AdminSession) {
  const canRead = session.permissions.includes("webhooks.read");
  const [lookupDraft, setLookupDraft] = useState<LookupDraft>(defaultLookupDraft);
  const [deliveries, setDeliveries] = useState<AdminDeliveryStatusView[]>([]);
  const [selectedDeliveryId, setSelectedDeliveryId] = useState<string | null>(null);
  const [notice, setNotice] = useState<DeliveryStatusWorkspaceNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const selectedDelivery = useMemo(
    () => deliveries.find((delivery) => delivery.deliveryId === selectedDeliveryId) ?? null,
    [deliveries, selectedDeliveryId],
  );

  async function loadDeliveries() {
    if (!canRead) {
      setNotice({
        tone: "danger",
        title: "Недостаточно прав",
        detail: "Для просмотра delivery outcomes нужен permission `webhooks.read`.",
      });
      return;
    }

    if (!lookupDraft.tenantId.trim()) {
      setNotice({
        tone: "danger",
        title: "Нужен tenantId",
        detail: "Укажите tenantId перед загрузкой recent deliveries.",
      });
      return;
    }

    const parsedLimit = Number.parseInt(lookupDraft.limit.trim() || "25", 10);
    if (!Number.isInteger(parsedLimit) || parsedLimit < 1 || parsedLimit > 200) {
      setNotice({
        tone: "danger",
        title: "Неверный limit",
        detail: "Limit должен быть целым числом от 1 до 200.",
      });
      return;
    }

    setPendingAction("load");
    try {
      const items = await adminApi.listDeliveryStatuses(lookupDraft.tenantId.trim(), {
        applicationClientId: lookupDraft.applicationClientId.trim() || undefined,
        channel: lookupDraft.channel || undefined,
        status: lookupDraft.status || undefined,
        limit: parsedLimit,
      });

      startTransition(() => {
        setDeliveries(items);
        setSelectedDeliveryId((current) => (
          items.some((item) => item.deliveryId === current)
            ? current
            : items[0]?.deliveryId ?? null
        ));
        setPendingAction(null);
        setNotice({
          tone: "success",
          title: "Delivery statuses loaded",
          detail: items.length === 0
            ? "Для выбранного tenant/filter recent deliveries пока не найдены."
            : `Загружено ${items.length} sanitized delivery outcome(s) для operator review.`,
        });
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  function selectDelivery(delivery: AdminDeliveryStatusView) {
    setSelectedDeliveryId(delivery.deliveryId);
  }

  function handleApiError(error: unknown) {
    const message = mapAdminError(error);
    startTransition(() => {
      setPendingAction(null);
      setNotice({
        tone: "danger",
        ...message,
      });
    });
  }

  return {
    lookupDraft,
    setLookupDraft,
    deliveries,
    selectedDeliveryId,
    selectedDelivery,
    notice,
    pendingAction,
    canRead,
    canLoad: canRead && Boolean(lookupDraft.tenantId.trim()),
    loadDeliveries,
    selectDelivery,
  };
}
