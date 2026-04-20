package ru.dt1520.security.authenticator.app.deviceruntime

import java.util.UUID
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval

internal data class DeviceActivationCommand(
    val tenantId: UUID,
    val externalUserId: String,
    val activationCode: String,
    val installationId: String,
    val deviceName: String? = null,
    val pushToken: String? = null,
    val publicKey: String? = null
)

internal data class DeviceTokenEnvelope(
    val accessToken: String,
    val refreshToken: String,
    val tokenType: String,
    val expiresInSeconds: Int,
    val scope: String
) {
    init {
        require(accessToken.isNotBlank()) {
            "accessToken must not be blank."
        }
        require(refreshToken.isNotBlank()) {
            "refreshToken must not be blank."
        }
        require(tokenType.isNotBlank()) {
            "tokenType must not be blank."
        }
        require(expiresInSeconds > 0) {
            "expiresInSeconds must be positive."
        }
        require(scope.isNotBlank()) {
            "scope must not be blank."
        }
    }
}

internal data class ActivatedDeviceSession(
    val deviceId: UUID,
    val tokens: DeviceTokenEnvelope
)

internal enum class DeviceRuntimeTransportFailureKind {
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Gone,
    Validation,
    Network,
    InvalidResponse,
    Server
}

internal class DeviceRuntimeTransportException(
    val kind: DeviceRuntimeTransportFailureKind,
    message: String,
    cause: Throwable? = null
) : IllegalStateException(message, cause)

internal class DeviceRuntimeSessionInvalidatedException(
    message: String,
    cause: Throwable? = null
) : IllegalStateException(message, cause)

internal interface DeviceRuntimeTransport {
    suspend fun activate(
        command: DeviceActivationCommand,
        integrationAccessToken: String
    ): ActivatedDeviceSession

    suspend fun refresh(refreshToken: String): DeviceTokenEnvelope

    suspend fun listPending(accessToken: String): List<PendingPushApproval>

    suspend fun approve(
        challengeId: UUID,
        deviceId: UUID,
        accessToken: String,
        biometricVerified: Boolean = true
    )

    suspend fun deny(
        challengeId: UUID,
        deviceId: UUID,
        accessToken: String,
        reason: String? = null
    )
}
