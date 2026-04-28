package ru.dt1520.security.authenticator.app.deviceonboarding

import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingPayload

internal sealed interface DeviceOnboardingRuntimeTarget {
    data class QrEnvelope(val baseUrl: String) : DeviceOnboardingRuntimeTarget

    data object ConfiguredFallback : DeviceOnboardingRuntimeTarget

    data object Missing : DeviceOnboardingRuntimeTarget
}

internal fun resolveDeviceOnboardingRuntimeTarget(
    payload: DeviceOnboardingPayload,
    configuredBaseUrl: String?
): DeviceOnboardingRuntimeTarget {
    val qrRuntimeBaseUrl = payload.runtimeBaseUrl
    if (!qrRuntimeBaseUrl.isNullOrBlank()) {
        return DeviceOnboardingRuntimeTarget.QrEnvelope(qrRuntimeBaseUrl)
    }

    if (!configuredBaseUrl.isNullOrBlank()) {
        return DeviceOnboardingRuntimeTarget.ConfiguredFallback
    }

    return DeviceOnboardingRuntimeTarget.Missing
}

internal fun resolveDefaultDeviceRuntimeBaseUrl(
    configuredBaseUrl: String?,
    persistedBaseUrl: String?
): String? {
    if (!configuredBaseUrl.isNullOrBlank()) {
        return configuredBaseUrl
    }

    return persistedBaseUrl?.takeIf(String::isNotBlank)
}
