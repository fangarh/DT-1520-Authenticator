package ru.dt1520.security.authenticator.app

import ru.dt1520.security.authenticator.app.deviceonboarding.DeviceOnboardingRuntimeTarget
import ru.dt1520.security.authenticator.app.deviceonboarding.resolveDeviceOnboardingRuntimeTarget
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeTransportException
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingActivationResult
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingPayload
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningDraft
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore

internal suspend fun activateDefaultDeviceOnboardingPayload(
    payload: DeviceOnboardingPayload,
    configuredBaseUrl: String?,
    defaultRuntimeManager: DeviceRuntimeSessionManager?,
    runtimeManagerOverride: DeviceRuntimeSessionManager?,
    runtimeManagerFactory: (String) -> DeviceRuntimeSessionManager,
    secureStore: SecureTotpSecretStore,
    deviceName: String,
    devicePushTokenResolver: suspend (DeviceRuntimeSessionManager) -> String?,
    updatePersistedRuntimeBaseUrl: (String?) -> Unit,
    refreshStoredSecrets: suspend () -> Unit
): DeviceOnboardingActivationResult {
    val runtimeTarget = resolveDeviceOnboardingRuntimeTarget(
        payload = payload,
        configuredBaseUrl = configuredBaseUrl
    )
    val runtimeManager = runtimeManagerOverride ?: when (runtimeTarget) {
        is DeviceOnboardingRuntimeTarget.QrEnvelope -> runtimeManagerFactory(runtimeTarget.baseUrl)
        DeviceOnboardingRuntimeTarget.ConfiguredFallback -> defaultRuntimeManager
        DeviceOnboardingRuntimeTarget.Missing -> null
    }
    val activationRuntimeBaseUrl = when (runtimeTarget) {
        is DeviceOnboardingRuntimeTarget.QrEnvelope -> runtimeTarget.baseUrl
        DeviceOnboardingRuntimeTarget.ConfiguredFallback -> configuredBaseUrl
        DeviceOnboardingRuntimeTarget.Missing -> null
    }

    if (runtimeManager == null) {
        return DeviceOnboardingActivationResult.Failure(
            userMessage = "QR не содержит runtime адрес. Отсканируйте QR, созданный в актуальной админ-панели."
        )
    }

    return runCatching {
        runtimeManager.activateWithOnboardingPayload(
            activationPayload = payload.activationPayload,
            deviceName = deviceName,
            pushToken = devicePushTokenResolver(runtimeManager)
        )
    }.fold(
        onSuccess = { deviceId ->
            updatePersistedRuntimeBaseUrl(activationRuntimeBaseUrl)
            importCombinedTotpProvisioningPayload(
                payload = payload.totpProvisioningPayload,
                secureStore = secureStore,
                refreshStoredSecrets = refreshStoredSecrets
            ).fold(
                onSuccess = { imported ->
                    DeviceOnboardingActivationResult.Success(
                        deviceId = deviceId,
                        totpImported = imported
                    )
                },
                onFailure = {
                    DeviceOnboardingActivationResult.PartialSuccess(
                        deviceId = deviceId,
                        userMessage = "Устройство подключено, но TOTP-код не удалось сохранить. Выпустите новый QR и повторите импорт."
                    )
                }
            )
        },
        onFailure = { exception ->
            DeviceOnboardingActivationResult.Failure(
                userMessage = exception.toDeviceOnboardingStatusMessage()
            )
        }
    )
}

internal suspend fun importCombinedTotpProvisioningPayload(
    payload: String?,
    secureStore: SecureTotpSecretStore,
    refreshStoredSecrets: suspend () -> Unit
): Result<Boolean> {
    if (payload.isNullOrBlank()) {
        return Result.success(false)
    }

    return runCatching {
        val preview = ProvisioningDraft(otpAuthUri = payload).buildOtpAuthPreview()
        secureStore.save(preview.toStoredSecret())
        refreshStoredSecrets()
        true
    }
}

private fun Throwable.toDeviceOnboardingStatusMessage(): String {
    return when (this) {
        is DeviceRuntimeTransportException ->
            "Не удалось подключить устройство. Проверьте срок действия QR и соединение."

        else ->
            "Не удалось подключить устройство. Проверьте QR и повторите попытку."
    }
}
