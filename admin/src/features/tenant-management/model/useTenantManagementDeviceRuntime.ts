import { startTransition, useMemo, useState } from "react";
import { adminApi } from "../../../shared/api/admin-api";
import { mapAdminError } from "../../../shared/problem/problem-messages";
import type {
  AdminDeliveryStatusView,
  AdminDeviceOnboardingArtifactView,
  AdminRuntimeConfigurationView,
  AdminSession,
  AdminTenantDirectoryDetailView,
  AdminUserDeviceView,
} from "../../../shared/types/admin-contracts";
import {
  createCombinedOnboardingQrEnvelopeValue,
  resolveDeviceOnboardingRuntimeBaseUrl,
} from "../../device-onboarding/model/deviceOnboardingQrEnvelope";
import {
  buildReportSummary,
  createUserDraft,
  parsePositiveInteger,
  upsertDevice,
  type TenantManagementNotice,
  type TenantManagementPendingAction,
  type TenantOneTimeQrPayload,
  type TenantUserDraft,
} from "./tenantManagementModel";

interface DeviceRuntimeOptions {
  session: AdminSession;
  directory: AdminTenantDirectoryDetailView;
  setNotice: (notice: TenantManagementNotice | null) => void;
  setPendingAction: (action: TenantManagementPendingAction) => void;
}

export function useTenantManagementDeviceRuntime(options: DeviceRuntimeOptions) {
  const { directory, session, setNotice, setPendingAction } = options;
  const tenantId = directory.tenant.tenantId;
  const canReadDevices = session.permissions.includes("devices.read");
  const canWriteDevices = session.permissions.includes("devices.write");
  const canWriteEnrollments = session.permissions.includes("enrollments.write");
  const canWriteCombinedOnboarding = canWriteDevices && canWriteEnrollments;
  const canReadDeliveries = session.permissions.includes("webhooks.read");
  const [userDraft, setUserDraft] = useState<TenantUserDraft>(() => createUserDraft(directory));
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [devices, setDevices] = useState<AdminUserDeviceView[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [deviceRevokeArmed, setDeviceRevokeArmed] = useState(false);
  const [oneTimeQrPayload, setOneTimeQrPayload] = useState<TenantOneTimeQrPayload | null>(null);
  const [issuedQrArtifacts, setIssuedQrArtifacts] = useState<AdminDeviceOnboardingArtifactView[]>([]);
  const [recentQrArtifacts, setRecentQrArtifacts] = useState<AdminDeviceOnboardingArtifactView[]>([]);
  const [runtimeConfiguration, setRuntimeConfiguration] = useState<AdminRuntimeConfigurationView | null>(null);
  const [deliveries, setDeliveries] = useState<AdminDeliveryStatusView[]>([]);

  const selectedDevice = useMemo(
    () => devices.find((device) => device.deviceId === selectedDeviceId) ?? null,
    [devices, selectedDeviceId],
  );
  const reportSummary = useMemo(
    () => buildReportSummary(devices, deliveries, recentQrArtifacts, issuedQrArtifacts),
    [deliveries, devices, issuedQrArtifacts, recentQrArtifacts],
  );

  function resetForDirectory(nextDirectory: AdminTenantDirectoryDetailView) {
    setUserDraft(createUserDraft(nextDirectory));
    setLoadedUserId(null);
    setDevices([]);
    setSelectedDeviceId(null);
    setDeviceRevokeArmed(false);
    setOneTimeQrPayload(null);
    setIssuedQrArtifacts([]);
    setRecentQrArtifacts([]);
    setDeliveries([]);
    setRuntimeConfiguration(null);
  }

  async function loadDevices() {
    if (!canReadDevices) return showNotice("danger", "Недостаточно прав", "Для просмотра devices нужен permission `devices.read`.");

    const externalUserId = userDraft.externalUserId.trim();
    if (!externalUserId) return showNotice("danger", "Нужен пользователь", "Укажите external user id.");

    setPendingAction("loadDevices");
    setOneTimeQrPayload(null);
    try {
      const items = await adminApi.listUserDevices(tenantId, externalUserId);
      startTransition(() => {
        setLoadedUserId(externalUserId);
        setDevices(items);
        setSelectedDeviceId(items[0]?.deviceId ?? null);
        setDeviceRevokeArmed(false);
        setPendingAction(null);
        showNotice("success", "User devices loaded", `Loaded ${items.length} device record(s) under selected tenant.`);
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function createQrArtifact() {
    if (!canWriteCombinedOnboarding) {
      return showNotice("danger", "Недостаточно прав", "Для combined QR нужны permissions `devices.write` и `enrollments.write`.");
    }

    const ttlMinutes = parsePositiveInteger(userDraft.ttlMinutes) ?? 0;
    const externalUserId = userDraft.externalUserId.trim();
    if (!externalUserId || !userDraft.applicationClientId.trim()) return showNotice("danger", "Нужен user/application context", "Укажите user и application для QR.");
    if (ttlMinutes <= 0) return showNotice("danger", "Неверный TTL", "TTL должен быть положительным числом минут.");

    setPendingAction("createQr");
    setOneTimeQrPayload(null);
    try {
      const response = await adminApi.createCombinedOnboardingPackage({
        tenantId,
        applicationClientId: userDraft.applicationClientId.trim(),
        externalUserId,
        platform: userDraft.platform,
        ttlMinutes,
        label: externalUserId,
      });
      const runtimeBaseUrl = resolveDeviceOnboardingRuntimeBaseUrl();
      const qrEnvelopeValue = createCombinedOnboardingQrEnvelopeValue({
        activationPayload: response.activationPayload,
        runtimeBaseUrl,
        totpProvisioningPayload: response.totpEnrollment.qrCodePayload ?? "",
      });

      startTransition(() => {
        setIssuedQrArtifacts((current) => [response.deviceArtifact, ...current]);
        setOneTimeQrPayload({
          activationCodeId: response.deviceArtifact.activationCodeId,
          activationPayload: response.activationPayload,
          runtimeBaseUrl,
          expiresAtUtc: response.deviceArtifact.expiresAtUtc,
          qrEnvelopeValue,
          mode: "combined",
          totpEnrollmentId: response.totpEnrollment.enrollmentId,
        });
        setPendingAction(null);
        showNotice("success", "Combined QR issued", "Device activation and TOTP provisioning material are current-session only.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function revokeDevice() {
    if (!canWriteDevices) return showNotice("danger", "Недостаточно прав", "Для revoke device нужен permission `devices.write`.");
    if (!loadedUserId || !selectedDevice) return showNotice("danger", "Device не выбран", "Загрузите пользователя и выберите active device.");
    if (selectedDevice.status !== "active" || !deviceRevokeArmed) return showNotice("neutral", "Revoke недоступен", "Device должен быть active и destructive action должен быть подтвержден.");

    setPendingAction("revokeDevice");
    try {
      const revoked = await adminApi.revokeUserDevice(tenantId, loadedUserId, selectedDevice.deviceId);
      startTransition(() => {
        setDevices((current) => upsertDevice(current, revoked));
        setSelectedDeviceId(revoked.deviceId);
        setDeviceRevokeArmed(false);
        setPendingAction(null);
        showNotice("success", "Device revoked", "Command used selected tenant/user/device context.");
      });
    } catch (error) {
      handleApiError(error);
    }
  }

  async function loadRuntimeConfiguration() {
    setPendingAction("loadRuntime");
    try {
      const configuration = await adminApi.getRuntimeConfiguration();
      setRuntimeConfiguration(configuration);
      setPendingAction(null);
      showNotice("success", "Runtime configuration loaded", "Callback policy metadata loaded without callback URLs or secrets.");
    } catch (error) {
      handleApiError(error);
    }
  }

  async function loadReports() {
    if (!canReadDeliveries) return showNotice("danger", "Недостаточно прав", "Для reports нужен permission `webhooks.read`.");

    setPendingAction("loadReports");
    try {
      const applicationClientId = userDraft.applicationClientId.trim() || undefined;
      const externalUserId = userDraft.externalUserId.trim() || undefined;
      const [deliveryItems, qrArtifacts] = await Promise.all([
        adminApi.listDeliveryStatuses(tenantId, {
          applicationClientId,
          limit: 25,
        }),
        canReadDevices
          ? adminApi.listDeviceOnboardingArtifacts(tenantId, {
            applicationClientId,
            externalUserId,
            limit: 25,
          })
          : Promise.resolve([]),
      ]);

      setDeliveries(deliveryItems);
      setRecentQrArtifacts(qrArtifacts);
      setPendingAction(null);
      showNotice("success", "Reports refreshed", `Loaded ${deliveryItems.length} delivery outcome(s) and ${qrArtifacts.length} QR artifact record(s) for selected tenant context.`);
    } catch (error) {
      handleApiError(error);
    }
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
    userDraft, setUserDraft, loadedUserId, devices, selectedDevice,
    selectedDeviceId, setSelectedDeviceId, deviceRevokeArmed, setDeviceRevokeArmed,
    oneTimeQrPayload, setOneTimeQrPayload, runtimeConfiguration, deliveries, recentQrArtifacts, reportSummary,
    canReadDevices, canWriteDevices, canWriteEnrollments, canWriteCombinedOnboarding, canReadDeliveries,
    resetForDirectory, loadDevices, createQrArtifact, revokeDevice,
    loadRuntimeConfiguration, loadReports,
  };
}
