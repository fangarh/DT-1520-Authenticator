import { startTransition, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type {
  AdminSession,
  UpsertWebhookSubscriptionRequest,
  WebhookEventType,
  WebhookSubscriptionView,
} from "../../../shared/types/admin-contracts";
import { webhookEventOptions } from "../../../shared/types/webhook-events";

export interface WebhookWorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

type PendingAction = "load" | "save" | null;

interface LookupDraft {
  tenantId: string;
  applicationClientId: string;
}

interface EditorDraft {
  applicationClientId: string;
  endpointUrl: string;
  eventTypes: WebhookEventType[];
  isActive: boolean;
}

function createEditorDraft(applicationClientId = ""): EditorDraft {
  return {
    applicationClientId,
    endpointUrl: "",
    eventTypes: ["challenge.approved"],
    isActive: true,
  };
}

function toEditorDraft(subscription: WebhookSubscriptionView): EditorDraft {
  return {
    applicationClientId: subscription.applicationClientId,
    endpointUrl: subscription.endpointUrl,
    eventTypes: subscription.eventTypes,
    isActive: subscription.status === "active",
  };
}

export function useWebhookSubscriptionWorkspace(session: AdminSession) {
  const canRead = session.permissions.includes("webhooks.read");
  const canWrite = session.permissions.includes("webhooks.write");
  const [lookupDraft, setLookupDraft] = useState<LookupDraft>({
    tenantId: "",
    applicationClientId: "",
  });
  const [editorDraft, setEditorDraft] = useState<EditorDraft>(createEditorDraft());
  const [subscriptions, setSubscriptions] = useState<WebhookSubscriptionView[]>([]);
  const [selectedSubscriptionId, setSelectedSubscriptionId] = useState<string | null>(null);
  const [notice, setNotice] = useState<WebhookWorkspaceNotice | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  async function loadSubscriptions() {
    if (!canRead) {
      setNotice({
        tone: "danger",
        title: "Недостаточно прав",
        detail: "Для загрузки subscriptions нужен permission `webhooks.read`.",
      });
      return;
    }

    if (!lookupDraft.tenantId.trim()) {
      setNotice({
        tone: "danger",
        title: "Нужен tenantId",
        detail: "Укажите tenantId перед загрузкой webhook subscriptions.",
      });
      return;
    }

    setPendingAction("load");
    try {
      const items = await adminApi.listWebhookSubscriptions(
        lookupDraft.tenantId.trim(),
        lookupDraft.applicationClientId.trim() || undefined,
      );
      startTransition(() => {
        setSubscriptions(items);
        setSelectedSubscriptionId((current) => items.some((item) => item.subscriptionId === current) ? current : null);
        setPendingAction(null);
        setNotice({
          tone: "success",
          title: "Subscriptions loaded",
          detail: items.length === 0
            ? "Для выбранного tenant filter подписки пока не найдены."
            : `Загружено ${items.length} subscription(s) для operator review.`,
        });
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function saveSubscription() {
    if (!canWrite) {
      setNotice({
        tone: "danger",
        title: "Недостаточно прав",
        detail: "Для изменения subscriptions нужен permission `webhooks.write`.",
      });
      return;
    }

    if (!lookupDraft.tenantId.trim()) {
      setNotice({
        tone: "danger",
        title: "Нужен tenantId",
        detail: "Сначала задайте tenantId для subscription management.",
      });
      return;
    }

    if (!editorDraft.endpointUrl.trim()) {
      setNotice({
        tone: "danger",
        title: "Нужен endpoint",
        detail: "Webhook endpoint URL обязателен.",
      });
      return;
    }

    if (editorDraft.eventTypes.length === 0) {
      setNotice({
        tone: "danger",
        title: "Нужны event types",
        detail: "Выберите хотя бы один platform event.",
      });
      return;
    }

    setPendingAction("save");
    try {
      const request: UpsertWebhookSubscriptionRequest = {
        tenantId: lookupDraft.tenantId.trim(),
        applicationClientId: (editorDraft.applicationClientId || lookupDraft.applicationClientId).trim() || undefined,
        endpointUrl: editorDraft.endpointUrl.trim(),
        eventTypes: editorDraft.eventTypes,
        isActive: editorDraft.isActive,
      };
      const subscription = await adminApi.upsertWebhookSubscription(request);
      const items = canRead
        ? await adminApi.listWebhookSubscriptions(
          lookupDraft.tenantId.trim(),
          lookupDraft.applicationClientId.trim() || undefined,
        )
        : [subscription];

      startTransition(() => {
        setSubscriptions(items);
        setSelectedSubscriptionId(subscription.subscriptionId);
        setEditorDraft(toEditorDraft(subscription));
        setPendingAction(null);
        setNotice({
          tone: "success",
          title: subscription.status === "active" ? "Subscription saved" : "Subscription deactivated",
          detail: subscription.status === "active"
            ? "Webhook subscription обновлена и активна для выбранных event types."
            : "Webhook subscription оставлена в базе, но больше не участвует в delivery fan-out.",
        });
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  function selectSubscription(subscription: WebhookSubscriptionView) {
    setSelectedSubscriptionId(subscription.subscriptionId);
    setEditorDraft(toEditorDraft(subscription));
    if (!lookupDraft.applicationClientId.trim()) {
      setLookupDraft((current) => ({
        ...current,
        applicationClientId: subscription.applicationClientId,
      }));
    }
  }

  function resetEditor() {
    setSelectedSubscriptionId(null);
    setEditorDraft(createEditorDraft(lookupDraft.applicationClientId.trim()));
    setNotice({
      tone: "neutral",
      title: "Editor reset",
      detail: "Форма очищена для новой subscription configuration.",
    });
  }

  function toggleEventType(eventType: WebhookEventType) {
    setEditorDraft((current) => {
      const hasEvent = current.eventTypes.includes(eventType);
      const nextEventTypes = hasEvent
        ? current.eventTypes.filter((item) => item !== eventType)
        : [...current.eventTypes, eventType];

      return {
        ...current,
        eventTypes: webhookEventOptions
          .map((item) => item.value)
          .filter((item) => nextEventTypes.includes(item)),
      };
    });
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
    editorDraft,
    setEditorDraft,
    subscriptions,
    selectedSubscriptionId,
    notice,
    pendingAction,
    canRead,
    canWrite,
    canLoad: canRead && Boolean(lookupDraft.tenantId.trim()),
    canSave: canWrite &&
      Boolean(lookupDraft.tenantId.trim()) &&
      Boolean(editorDraft.endpointUrl.trim()) &&
      editorDraft.eventTypes.length > 0,
    loadSubscriptions,
    saveSubscription,
    selectSubscription,
    resetEditor,
    toggleEventType,
  };
}
