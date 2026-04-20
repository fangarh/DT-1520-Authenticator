package ru.dt1520.security.authenticator.app.pushapprovals

import java.time.Instant
import java.util.UUID
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionInvalidatedException
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryDecision
import ru.dt1520.security.authenticator.security.storage.SecurePushDecisionHistoryStore
import ru.dt1520.security.authenticator.security.storage.StoredPushDecisionHistoryDecision
import ru.dt1520.security.authenticator.security.storage.StoredPushDecisionHistoryEntry

class PushApprovalDecisionCoordinatorTest {
    @Test
    fun approveRequiresBiometricVerificationBeforeCallingRuntime() = runBlocking {
        val runtime = RecordingPushApprovalRuntime()
        val historyStore = InMemoryPushDecisionHistoryStore()
        val coordinator = DefaultPushApprovalDecisionCoordinator(
            runtime = runtime,
            biometricGate = FakePushApprovalBiometricGate(
                result = PushApprovalBiometricGateResult.Rejected(
                    "Локальное подтверждение было отменено."
                )
            ),
            historyStore = historyStore,
            currentInstantProvider = { Instant.parse("2026-04-17T12:00:00Z") }
        )

        val result = coordinator.approve(CHALLENGE)

        assertEquals(
            PushApprovalDecisionResult.Failure("Локальное подтверждение было отменено."),
            result
        )
        assertEquals(0, runtime.approveInvocations)
        assertTrue(historyStore.entries.isEmpty())
    }

    @Test
    fun approvePersistsSanitizedHistoryAfterSuccessfulDecision() = runBlocking {
        val runtime = RecordingPushApprovalRuntime()
        val historyStore = InMemoryPushDecisionHistoryStore()
        val coordinator = DefaultPushApprovalDecisionCoordinator(
            runtime = runtime,
            biometricGate = FakePushApprovalBiometricGate(
                result = PushApprovalBiometricGateResult.Verified
            ),
            historyStore = historyStore,
            currentInstantProvider = { Instant.parse("2026-04-17T12:00:00Z") }
        )

        val result = coordinator.approve(CHALLENGE)

        assertEquals(PushApprovalDecisionResult.Success, result)
        assertEquals(1, runtime.approveInvocations)
        assertEquals(1, historyStore.entries.size)
        assertEquals("login", historyStore.entries.single().operationType)
        assertEquals(StoredPushDecisionHistoryDecision.Approved, historyStore.entries.single().decision)
        assertEquals("operator@example.local", historyStore.entries.single().username)
    }

    @Test
    fun denyPersistsHistoryWithoutBiometricGate() = runBlocking {
        val runtime = RecordingPushApprovalRuntime()
        val historyStore = InMemoryPushDecisionHistoryStore()
        val coordinator = DefaultPushApprovalDecisionCoordinator(
            runtime = runtime,
            biometricGate = FakePushApprovalBiometricGate(
                result = PushApprovalBiometricGateResult.Verified
            ),
            historyStore = historyStore,
            currentInstantProvider = { Instant.parse("2026-04-17T12:30:00Z") }
        )

        val result = coordinator.deny(CHALLENGE)

        assertEquals(PushApprovalDecisionResult.Success, result)
        assertEquals(1, runtime.denyInvocations)
        assertEquals(StoredPushDecisionHistoryDecision.Denied, historyStore.entries.single().decision)
    }

    @Test
    fun sessionInvalidationReturnsFailureThatClearsPendingState() = runBlocking {
        val runtime = RecordingPushApprovalRuntime().apply {
            invalidateOnApprove = true
        }
        val coordinator = DefaultPushApprovalDecisionCoordinator(
            runtime = runtime,
            biometricGate = FakePushApprovalBiometricGate(
                result = PushApprovalBiometricGateResult.Verified
            ),
            historyStore = InMemoryPushDecisionHistoryStore()
        )

        val result = coordinator.approve(CHALLENGE)

        assertEquals(
            PushApprovalDecisionResult.Failure(
                userMessage = "Сессия устройства истекла, отозвана или заблокирована. Привяжите устройство заново.",
                statusMessage = "Сессия устройства истекла, отозвана или заблокирована. Привяжите устройство заново.",
                shouldClearPendingChallenges = true
            ),
            result
        )
    }

    @Test
    fun listDecisionHistoryMapsStoredEntriesIntoUiModel() = runBlocking {
        val historyStore = InMemoryPushDecisionHistoryStore().apply {
            entries += StoredPushDecisionHistoryEntry(
                operationType = "step_up",
                operationDisplayName = "VPN access",
                username = "operator@example.local",
                decision = StoredPushDecisionHistoryDecision.Denied,
                decidedAtEpochSeconds = Instant.parse("2026-04-17T12:00:00Z").epochSecond
            )
        }
        val coordinator = DefaultPushApprovalDecisionCoordinator(
            runtime = RecordingPushApprovalRuntime(),
            biometricGate = FakePushApprovalBiometricGate(
                result = PushApprovalBiometricGateResult.Verified
            ),
            historyStore = historyStore
        )

        val history = coordinator.listDecisionHistory()

        assertEquals(1, history.size)
        assertEquals("VPN access", history.single().operationDisplayName)
        assertEquals(PushDecisionHistoryDecision.Denied, history.single().decision)
    }

    private class RecordingPushApprovalRuntime : PushApprovalDeviceRuntime {
        var approveInvocations: Int = 0
        var denyInvocations: Int = 0
        var invalidateOnApprove: Boolean = false

        override suspend fun approvePushChallenge(challenge: PendingPushApproval) {
            approveInvocations += 1
            if (invalidateOnApprove) {
                throw DeviceRuntimeSessionInvalidatedException("invalidated")
            }
        }

        override suspend fun denyPushChallenge(challenge: PendingPushApproval) {
            denyInvocations += 1
        }
    }

    private class FakePushApprovalBiometricGate(
        private val result: PushApprovalBiometricGateResult
    ) : PushApprovalBiometricGate {
        override suspend fun verifyApproval(
            challenge: PendingPushApproval
        ): PushApprovalBiometricGateResult = result
    }

    private class InMemoryPushDecisionHistoryStore : SecurePushDecisionHistoryStore {
        val entries = mutableListOf<StoredPushDecisionHistoryEntry>()

        override suspend fun list(limit: Int): List<StoredPushDecisionHistoryEntry> =
            entries.take(limit)

        override suspend fun append(entry: StoredPushDecisionHistoryEntry, limit: Int) {
            entries.add(0, entry)
            while (entries.size > limit) {
                entries.removeLast()
            }
        }
    }

    private companion object {
        val CHALLENGE = PendingPushApproval(
            id = UUID.fromString("1cc36d18-e8aa-4ed7-b951-6f589b0893fd"),
            operationType = "login",
            username = "operator@example.local",
            expiresAt = Instant.parse("2026-04-17T12:05:00Z")
        )
    }
}
