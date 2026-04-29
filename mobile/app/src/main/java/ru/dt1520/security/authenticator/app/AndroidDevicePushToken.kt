package ru.dt1520.security.authenticator.app

import ru.dt1520.security.authenticator.BuildConfig
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager

internal suspend fun resolveDevicePushTokenForCurrentBuild(
    runtimeManager: DeviceRuntimeSessionManager
): String? {
    if (!BuildConfig.DEBUG) {
        return null
    }

    return DebugDevicePushToken.fromInstallationId(runtimeManager.getOrCreateInstallationId())
}

internal object DebugDevicePushToken {
    fun fromInstallationId(installationId: String): String? {
        val normalizedInstallationId = installationId.trim()
        if (normalizedInstallationId.isBlank()) {
            return null
        }

        return "dt1520-debug-android-$normalizedInstallationId"
    }
}
