package ru.dt1520.security.authenticator.app.deviceruntime

import java.time.Instant
import java.util.UUID
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.security.storage.SecureDeviceSessionStore
import ru.dt1520.security.authenticator.security.storage.StoredDeviceInstallation
import ru.dt1520.security.authenticator.security.storage.StoredDeviceSession

class DeviceRuntimeSessionManagerTest {
    @Test
    fun activatePersistsInstallationAndIssuedSession() = runBlocking {
        val store = InMemorySecureDeviceSessionStore()
        val transport = RecordingDeviceRuntimeTransport()
        val manager = DeviceRuntimeSessionManager(
            sessionStore = store,
            transport = transport,
            currentEpochSecondsProvider = { 1_700_000_000L },
            installationIdFactory = { "installation-generated" }
        )

        val deviceId = manager.activate(
            tenantId = TENANT_ID,
            externalUserId = "operator@example.local",
            activationCode = "activation-code",
            integrationAccessToken = "integration-access-token",
            deviceName = "Pixel 10 Pro"
        )

        assertEquals(DEVICE_ID, deviceId)
        assertEquals("installation-generated", store.installation?.installationId)
        assertEquals("access-1", store.session?.accessToken)
        assertEquals("refresh-1", store.session?.refreshToken)
        assertEquals(1_700_000_900L, store.session?.accessTokenExpiresAtEpochSeconds)
        assertEquals("Bearer integration-access-token", transport.lastIntegrationAuthorization)
        assertEquals("installation-generated", transport.lastActivationCommand?.installationId)
    }

    @Test
    fun listPendingReturnsEmptyWhenSessionIsMissing() = runBlocking {
        val manager = DeviceRuntimeSessionManager(
            sessionStore = InMemorySecureDeviceSessionStore(),
            transport = RecordingDeviceRuntimeTransport()
        )

        val approvals = manager.listPendingPushApprovals()

        assertTrue(approvals.isEmpty())
    }

    @Test
    fun listPendingRefreshesExpiredSessionBeforeCallingProtectedEndpoint() = runBlocking {
        val store = InMemorySecureDeviceSessionStore(
            installation = StoredDeviceInstallation("installation-1234"),
            session = StoredDeviceSession(
                deviceId = DEVICE_ID,
                accessToken = "expired-access",
                refreshToken = "refresh-1",
                tokenType = "Bearer",
                scope = "challenge",
                accessTokenExpiresAtEpochSeconds = 1_700_000_010L
            )
        )
        val transport = RecordingDeviceRuntimeTransport().apply {
            pendingApprovals = listOf(
                PendingPushApproval(
                    id = UUID.randomUUID(),
                    operationType = "login",
                    expiresAt = Instant.parse("2026-04-17T12:00:00Z")
                )
            )
        }
        val manager = DeviceRuntimeSessionManager(
            sessionStore = store,
            transport = transport,
            currentEpochSecondsProvider = { 1_700_000_050L }
        )

        val approvals = manager.listPendingPushApprovals()

        assertEquals(1, approvals.size)
        assertEquals(1, transport.refreshInvocations)
        assertEquals("Bearer access-2", transport.lastPendingAuthorization)
        assertEquals("access-2", store.session?.accessToken)
        assertEquals("refresh-2", store.session?.refreshToken)
    }

    @Test
    fun protectedRequestRetriesOnceAfterUnauthorizedUsingRotatedSession() = runBlocking {
        val store = InMemorySecureDeviceSessionStore(
            installation = StoredDeviceInstallation("installation-1234"),
            session = StoredDeviceSession(
                deviceId = DEVICE_ID,
                accessToken = "access-1",
                refreshToken = "refresh-1",
                tokenType = "Bearer",
                scope = "challenge",
                accessTokenExpiresAtEpochSeconds = 1_700_000_900L
            )
        )
        val transport = RecordingDeviceRuntimeTransport().apply {
            failNextPendingWithUnauthorized = true
            pendingApprovals = listOf(
                PendingPushApproval(
                    id = UUID.randomUUID(),
                    operationType = "step_up",
                    expiresAt = Instant.parse("2026-04-17T12:00:00Z")
                )
            )
        }
        val manager = DeviceRuntimeSessionManager(
            sessionStore = store,
            transport = transport,
            currentEpochSecondsProvider = { 1_700_000_000L }
        )

        val approvals = manager.listPendingPushApprovals()

        assertEquals(1, approvals.size)
        assertEquals(1, transport.refreshInvocations)
        assertEquals(2, transport.pendingInvocations)
        assertEquals("access-2", store.session?.accessToken)
    }

    @Test(expected = DeviceRuntimeSessionInvalidatedException::class)
    fun refreshConflictClearsStoredSessionFailClosed() {
        val store = InMemorySecureDeviceSessionStore(
            installation = StoredDeviceInstallation("installation-1234"),
            session = StoredDeviceSession(
                deviceId = DEVICE_ID,
                accessToken = "expired-access",
                refreshToken = "refresh-1",
                tokenType = "Bearer",
                scope = "challenge",
                accessTokenExpiresAtEpochSeconds = 1_700_000_010L
            )
        )
        val transport = RecordingDeviceRuntimeTransport().apply {
            refreshFailure = DeviceRuntimeTransportException(
                kind = DeviceRuntimeTransportFailureKind.Conflict,
                message = "Refresh token is invalid or expired."
            )
        }
        val manager = DeviceRuntimeSessionManager(
            sessionStore = store,
            transport = transport,
            currentEpochSecondsProvider = { 1_700_000_050L }
        )

        try {
            runBlocking {
                manager.listPendingPushApprovals()
            }
        } finally {
            assertEquals(null, store.session)
        }
    }

    private class InMemorySecureDeviceSessionStore(
        var installation: StoredDeviceInstallation? = null,
        var session: StoredDeviceSession? = null
    ) : SecureDeviceSessionStore {
        override suspend fun readInstallation(): StoredDeviceInstallation? = installation

        override suspend fun saveInstallation(installation: StoredDeviceInstallation) {
            this.installation = installation
        }

        override suspend fun readSession(): StoredDeviceSession? = session

        override suspend fun saveSession(session: StoredDeviceSession) {
            this.session = session
        }

        override suspend fun clearSession() {
            session = null
        }
    }

    private class RecordingDeviceRuntimeTransport : DeviceRuntimeTransport {
        var lastIntegrationAuthorization: String? = null
        var lastActivationCommand: DeviceActivationCommand? = null
        var lastPendingAuthorization: String? = null
        var pendingInvocations: Int = 0
        var refreshInvocations: Int = 0
        var failNextPendingWithUnauthorized: Boolean = false
        var refreshFailure: DeviceRuntimeTransportException? = null
        var pendingApprovals: List<PendingPushApproval> = emptyList()

        override suspend fun activate(
            command: DeviceActivationCommand,
            integrationAccessToken: String
        ): ActivatedDeviceSession {
            lastActivationCommand = command
            lastIntegrationAuthorization = "Bearer $integrationAccessToken"
            return ActivatedDeviceSession(
                deviceId = DEVICE_ID,
                tokens = DeviceTokenEnvelope(
                    accessToken = "access-1",
                    refreshToken = "refresh-1",
                    tokenType = "Bearer",
                    expiresInSeconds = 900,
                    scope = "challenge"
                )
            )
        }

        override suspend fun refresh(refreshToken: String): DeviceTokenEnvelope {
            refreshInvocations += 1
            refreshFailure?.let { throw it }
            return DeviceTokenEnvelope(
                accessToken = "access-2",
                refreshToken = "refresh-2",
                tokenType = "Bearer",
                expiresInSeconds = 900,
                scope = "challenge"
            )
        }

        override suspend fun listPending(accessToken: String): List<PendingPushApproval> {
            pendingInvocations += 1
            lastPendingAuthorization = accessToken
            if (failNextPendingWithUnauthorized) {
                failNextPendingWithUnauthorized = false
                throw DeviceRuntimeTransportException(
                    kind = DeviceRuntimeTransportFailureKind.Unauthorized,
                    message = "Authenticated principal is missing device claims."
                )
            }

            return pendingApprovals
        }

        override suspend fun approve(
            challengeId: UUID,
            deviceId: UUID,
            accessToken: String,
            biometricVerified: Boolean
        ) = Unit

        override suspend fun deny(
            challengeId: UUID,
            deviceId: UUID,
            accessToken: String,
            reason: String?
        ) = Unit
    }

    private companion object {
        val TENANT_ID: UUID = UUID.fromString("924cecc1-b0bc-48f1-8502-b5fe5b6ff62f")
        val DEVICE_ID: UUID = UUID.fromString("73092591-d55c-49a6-bb50-6d7d64d32499")
    }
}
