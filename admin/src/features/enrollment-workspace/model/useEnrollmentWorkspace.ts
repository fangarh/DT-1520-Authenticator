import { startTransition, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type { AdminSession, TotpEnrollmentCurrent } from "../../../shared/types/admin-contracts";
import { parseProvisioningArtifact, type ProvisioningArtifact } from "./provisioning-artifact";

export interface WorkspaceNotice {
  tone: "neutral" | "success" | "danger";
  title: string;
  detail: string;
  actionHint?: string;
}

type PendingAction = "lookup" | "start" | "confirm" | "replace" | "revoke" | null;

export function useEnrollmentWorkspace(_session: AdminSession) {
  const [lookupDraft, setLookupDraft] = useState({
    tenantId: "",
    externalUserId: "",
  });
  const [startDraft, setStartDraft] = useState({
    applicationClientId: "",
    issuer: "OTPAuth",
    label: "",
  });
  const [confirmCode, setConfirmCode] = useState("");
  const [current, setCurrent] = useState<TotpEnrollmentCurrent | null>(null);
  const [artifact, setArtifact] = useState<ProvisioningArtifact | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const [notice, setNotice] = useState<WorkspaceNotice | null>(null);

  async function lookupCurrent() {
    if (!lookupDraft.tenantId.trim() || !lookupDraft.externalUserId.trim()) {
      setNotice({
        tone: "danger",
        title: "Нужны идентификаторы",
        detail: "Укажите tenantId и externalUserId перед lookup.",
      });
      return;
    }

    setPendingAction("lookup");
    try {
      const enrollment = await adminApi.getCurrentEnrollment(lookupDraft.tenantId.trim(), lookupDraft.externalUserId.trim());
      startTransition(() => {
        setCurrent(enrollment);
        setArtifact(null);
        setPendingAction(null);
        setNotice({
          tone: "success",
          title: "Current state loaded",
          detail: `Enrollment ${enrollment.enrollmentId} доступен для operator actions.`,
        });
      });
    } catch (error) {
      handleApiError(error, "lookup", true);
    }
  }

  async function startEnrollment() {
    if (!lookupDraft.tenantId.trim() || !lookupDraft.externalUserId.trim()) {
      setNotice({
        tone: "danger",
        title: "Нужны идентификаторы",
        detail: "Сначала задайте tenantId и externalUserId.",
      });
      return;
    }

    setPendingAction("start");
    try {
      const response = await adminApi.startEnrollment({
        tenantId: lookupDraft.tenantId.trim(),
        externalUserId: lookupDraft.externalUserId.trim(),
        applicationClientId: startDraft.applicationClientId.trim() || undefined,
        issuer: startDraft.issuer.trim() || undefined,
        label: startDraft.label.trim() || undefined,
      });
      const nextArtifact = parseProvisioningArtifact(response);
      await reloadCurrent({
        nextArtifact,
        success: {
          tone: "success",
          title: "Enrollment started",
          detail: "Provisioning artifact получен только для этой operator session.",
        },
      });
    } catch (error) {
      handleApiError(error, "start", false);
    }
  }

  async function confirmEnrollment() {
    const enrollmentId = current?.enrollmentId ?? artifact?.enrollmentId;
    if (!enrollmentId) {
      setNotice({
        tone: "danger",
        title: "Нечего подтверждать",
        detail: "Сначала выполните lookup или start/replacement flow.",
      });
      return;
    }

    setPendingAction("confirm");
    try {
      await adminApi.confirmEnrollment(enrollmentId, { code: confirmCode.trim() });
      setConfirmCode("");
      await reloadCurrent({
        nextArtifact: null,
        success: {
          tone: "success",
          title: "Enrollment confirmed",
          detail: "Provisioning artifact скрыт, current state обновлен.",
        },
      });
    } catch (error) {
      handleApiError(error, "confirm", false);
    }
  }

  async function replaceEnrollment() {
    if (!current?.enrollmentId) {
      return;
    }

    setPendingAction("replace");
    try {
      const response = await adminApi.replaceEnrollment(current.enrollmentId);
      const nextArtifact = parseProvisioningArtifact(response);
      await reloadCurrent({
        nextArtifact,
        success: {
          tone: "success",
          title: "Replacement started",
          detail: "Старый фактор остается активным до успешного confirm.",
        },
      });
    } catch (error) {
      handleApiError(error, "replace", false);
    }
  }

  async function revokeEnrollment() {
    if (!current?.enrollmentId) {
      return;
    }

    setPendingAction("revoke");
    try {
      await adminApi.revokeEnrollment(current.enrollmentId);
      await reloadCurrent({
        nextArtifact: null,
        success: {
          tone: "success",
          title: "Enrollment revoked",
          detail: "Текущий enrollment переведен в revoked и replacement artifacts сброшены.",
        },
      });
    } catch (error) {
      handleApiError(error, "revoke", false);
    }
  }

  async function reloadCurrent(options: { nextArtifact: ProvisioningArtifact | null; success: WorkspaceNotice }) {
    const enrollment = await adminApi.getCurrentEnrollment(lookupDraft.tenantId.trim(), lookupDraft.externalUserId.trim());
    startTransition(() => {
      setCurrent(enrollment);
      setArtifact(options.nextArtifact);
      setPendingAction(null);
      setNotice(options.success);
    });
  }

  function handleApiError(error: unknown, action: PendingAction, clearCurrent: boolean) {
    const message = mapAdminError(error);
    startTransition(() => {
      if (clearCurrent) {
        setCurrent(null);
      }

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
    startDraft,
    setStartDraft,
    confirmCode,
    setConfirmCode,
    current,
    artifact,
    notice,
    pendingAction,
    lookupCurrent,
    startEnrollment,
    confirmEnrollment,
    replaceEnrollment,
    revokeEnrollment,
    canReplace: current?.status === "confirmed",
    canRevoke: current?.status === "pending" || current?.status === "confirmed",
    canConfirm: current?.status === "pending" || current?.hasPendingReplacement || Boolean(artifact),
  };
}
