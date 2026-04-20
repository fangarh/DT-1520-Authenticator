package ru.dt1520.security.authenticator.app.deviceruntime

import java.util.UUID
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.security.storage.SecureDeviceSessionStore
import ru.dt1520.security.authenticator.security.storage.StoredDeviceInstallation
import ru.dt1520.security.authenticator.security.storage.StoredDeviceSession

internal class DeviceRuntimeSessionManager(
    private val sessionStore: SecureDeviceSessionStore,
    private val transport: DeviceRuntimeTransport,
    private val currentEpochSecondsProvider: () -> Long = { System.currentTimeMillis() / 1_000L },
    private val installationIdFactory: () -> String = { UUID.randomUUID().toString() },
    private val refreshSkewSeconds: Long = DEFAULT_REFRESH_SKEW_SECONDS
) {
    suspend fun getOrCreateInstallationId(): String {
        val existingInstallation = sessionStore.readInstallation()
        if (existingInstallation != null) {
            return existingInstallation.installationId
        }

        val installation = StoredDeviceInstallation(installationIdFactory())
        sessionStore.saveInstallation(installation)
        return installation.installationId
    }

    suspend fun activate(
        tenantId: UUID,
        externalUserId: String,
        activationCode: String,
        integrationAccessToken: String,
        deviceName: String? = null,
        pushToken: String? = null,
        publicKey: String? = null
    ): UUID {
        val activation = transport.activate(
            command = DeviceActivationCommand(
                tenantId = tenantId,
                externalUserId = externalUserId,
                activationCode = activationCode,
                installationId = getOrCreateInstallationId(),
                deviceName = deviceName,
                pushToken = pushToken,
                publicKey = publicKey
            ),
            integrationAccessToken = integrationAccessToken
        )

        sessionStore.saveSession(
            StoredDeviceSession(
                deviceId = activation.deviceId,
                accessToken = activation.tokens.accessToken,
                refreshToken = activation.tokens.refreshToken,
                tokenType = activation.tokens.tokenType,
                scope = activation.tokens.scope,
                accessTokenExpiresAtEpochSeconds = currentEpochSecondsProvider() + activation.tokens.expiresInSeconds
            )
        )

        return activation.deviceId
    }

    suspend fun listPendingPushApprovals(): List<PendingPushApproval> {
        val session = sessionStore.readSession()
            ?.toRuntimeSession()
            ?: return emptyList()

        return withAuthorizedSession(session) { authorizedSession ->
            transport.listPending(authorizedSession.authorizationHeader)
        }
    }

    suspend fun approvePushChallenge(challenge: PendingPushApproval) {
        withRequiredSession { session ->
            withAuthorizedSession(session) { authorizedSession ->
                transport.approve(
                    challengeId = challenge.id,
                    deviceId = authorizedSession.deviceId,
                    accessToken = authorizedSession.authorizationHeader,
                    biometricVerified = true
                )
            }
        }
    }

    suspend fun denyPushChallenge(
        challenge: PendingPushApproval,
        reason: String? = null
    ) {
        withRequiredSession { session ->
            withAuthorizedSession(session) { authorizedSession ->
                transport.deny(
                    challengeId = challenge.id,
                    deviceId = authorizedSession.deviceId,
                    accessToken = authorizedSession.authorizationHeader,
                    reason = reason
                )
            }
        }
    }

    private suspend fun <T> withRequiredSession(
        block: suspend (RuntimeDeviceSession) -> T
    ): T {
        val session = sessionStore.readSession()
            ?.toRuntimeSession()
            ?: throw DeviceRuntimeSessionInvalidatedException(
                "Device session is not available on this installation."
            )

        return block(session)
    }

    private suspend fun <T> withAuthorizedSession(
        session: RuntimeDeviceSession,
        operation: suspend (RuntimeDeviceSession) -> T
    ): T {
        val initialSession = if (session.shouldRefresh(currentEpochSecondsProvider(), refreshSkewSeconds)) {
            refreshSession(session)
        } else {
            session
        }

        return try {
            operation(initialSession)
        } catch (exception: DeviceRuntimeTransportException) {
            if (!exception.shouldRetryWithRefresh()) {
                throw exception
            }

            val refreshedSession = refreshSession(initialSession)
            try {
                operation(refreshedSession)
            } catch (retryException: DeviceRuntimeTransportException) {
                if (retryException.shouldInvalidateSession()) {
                    sessionStore.clearSession()
                    throw DeviceRuntimeSessionInvalidatedException(
                        "Device session is no longer valid after retry.",
                        retryException
                    )
                }

                throw retryException
            }
        }
    }

    private suspend fun refreshSession(session: RuntimeDeviceSession): RuntimeDeviceSession {
        val tokens = try {
            transport.refresh(session.refreshToken)
        } catch (exception: DeviceRuntimeTransportException) {
            if (exception.shouldInvalidateSession()) {
                sessionStore.clearSession()
                throw DeviceRuntimeSessionInvalidatedException(
                    "Device session is no longer valid for refresh.",
                    exception
                )
            }

            throw exception
        }

        val refreshedSession = session.copy(
            accessToken = tokens.accessToken,
            refreshToken = tokens.refreshToken,
            tokenType = tokens.tokenType,
            scope = tokens.scope,
            accessTokenExpiresAtEpochSeconds = currentEpochSecondsProvider() + tokens.expiresInSeconds
        )
        sessionStore.saveSession(refreshedSession.toStoredSession())
        return refreshedSession
    }

    private data class RuntimeDeviceSession(
        val deviceId: UUID,
        val accessToken: String,
        val refreshToken: String,
        val tokenType: String,
        val scope: String,
        val accessTokenExpiresAtEpochSeconds: Long
    ) {
        val authorizationHeader: String
            get() = "$tokenType $accessToken"

        fun shouldRefresh(
            currentEpochSeconds: Long,
            refreshSkewSeconds: Long
        ): Boolean = accessTokenExpiresAtEpochSeconds <= currentEpochSeconds + refreshSkewSeconds
    }

    private fun StoredDeviceSession.toRuntimeSession(): RuntimeDeviceSession = RuntimeDeviceSession(
        deviceId = deviceId,
        accessToken = accessToken,
        refreshToken = refreshToken,
        tokenType = tokenType,
        scope = scope,
        accessTokenExpiresAtEpochSeconds = accessTokenExpiresAtEpochSeconds
    )

    private fun RuntimeDeviceSession.toStoredSession(): StoredDeviceSession = StoredDeviceSession(
        deviceId = deviceId,
        accessToken = accessToken,
        refreshToken = refreshToken,
        tokenType = tokenType,
        scope = scope,
        accessTokenExpiresAtEpochSeconds = accessTokenExpiresAtEpochSeconds
    )

    private fun DeviceRuntimeTransportException.shouldRetryWithRefresh(): Boolean {
        return kind == DeviceRuntimeTransportFailureKind.Unauthorized ||
            kind == DeviceRuntimeTransportFailureKind.Forbidden
    }

    private fun DeviceRuntimeTransportException.shouldInvalidateSession(): Boolean {
        return kind == DeviceRuntimeTransportFailureKind.Unauthorized ||
            kind == DeviceRuntimeTransportFailureKind.Forbidden ||
            kind == DeviceRuntimeTransportFailureKind.Conflict
    }

    private companion object {
        const val DEFAULT_REFRESH_SKEW_SECONDS: Long = 30L
    }
}
