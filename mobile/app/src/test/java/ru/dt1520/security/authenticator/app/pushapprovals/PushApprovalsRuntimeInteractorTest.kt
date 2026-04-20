package ru.dt1520.security.authenticator.app.pushapprovals

import java.time.Instant
import java.util.UUID
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryDecision
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryEntry

class PushApprovalsRuntimeInteractorTest {
    @Test
    fun loadPendingChallengesReturnsFallbackWhenRuntimeSyncIsNotConfigured() = runBlocking {
        val fallbackChallenges = listOf(sampleChallenge())
        val interactor = PushApprovalsRuntimeInteractor()

        val result = interactor.loadPendingChallenges(fallbackChallenges)

        assertEquals(fallbackChallenges, result.challenges)
        assertEquals(null, result.statusMessage)
    }

    @Test
    fun loadPendingChallengesFailsClosedWhenRuntimeSyncThrows() = runBlocking {
        val interactor = PushApprovalsRuntimeInteractor(
            listPendingChallenges = {
                throw IllegalStateException("network down")
            }
        )

        val result = interactor.loadPendingChallenges(listOf(sampleChallenge()))

        assertTrue(result.challenges.isEmpty())
        assertEquals(
            "Не удалось синхронизировать ожидающие push-запросы. Повторите попытку позже.",
            result.statusMessage
        )
    }

    @Test
    fun approveRefreshesPendingChallengesAndHistoryAfterSuccessfulDecision() = runBlocking {
        val challenge = sampleChallenge()
        val historyEntry = PushDecisionHistoryEntry(
            operationType = challenge.operationType,
            operationDisplayName = challenge.operationDisplayName,
            username = challenge.username,
            decision = PushDecisionHistoryDecision.Approved,
            decidedAt = Instant.parse("2026-04-17T12:00:00Z")
        )
        val interactor = PushApprovalsRuntimeInteractor(
            listPendingChallenges = { emptyList() },
            listDecisionHistory = { listOf(historyEntry) },
            approveChallenge = { PushApprovalDecisionResult.Success }
        )

        val update = interactor.approve(
            challenge = challenge,
            currentPendingChallenges = listOf(challenge),
            currentDecisionHistory = emptyList()
        )

        assertEquals(PushApprovalDecisionResult.Success, update.result)
        assertTrue(update.pendingChallenges.isEmpty())
        assertEquals(listOf(historyEntry), update.decisionHistory)
        assertEquals(null, update.statusMessage)
    }

    @Test
    fun approvePreservesHistoryAndClearsPendingWhenDecisionRequestsSessionReset() = runBlocking {
        val challenge = sampleChallenge()
        val existingHistory = listOf(
            PushDecisionHistoryEntry(
                operationType = "step_up",
                operationDisplayName = "VPN access",
                username = "operator@example.local",
                decision = PushDecisionHistoryDecision.Denied,
                decidedAt = Instant.parse("2026-04-17T11:00:00Z")
            )
        )
        val failure = PushApprovalDecisionResult.Failure(
            userMessage = "Сессия устройства истекла, отозвана или заблокирована. Привяжите устройство заново.",
            statusMessage = "Сессия устройства истекла, отозвана или заблокирована. Привяжите устройство заново.",
            shouldClearPendingChallenges = true
        )
        val interactor = PushApprovalsRuntimeInteractor(
            approveChallenge = { failure }
        )

        val update = interactor.approve(
            challenge = challenge,
            currentPendingChallenges = listOf(challenge),
            currentDecisionHistory = existingHistory
        )

        assertEquals(failure, update.result)
        assertTrue(update.pendingChallenges.isEmpty())
        assertEquals(existingHistory, update.decisionHistory)
        assertEquals(failure.statusMessage, update.statusMessage)
    }

    private companion object {
        fun sampleChallenge(): PendingPushApproval = PendingPushApproval(
            id = UUID.fromString("5cfa0ab1-6aa6-4c5c-a95d-544eb3c4774e"),
            operationType = "login",
            username = "operator@example.local",
            expiresAt = Instant.parse("2026-04-17T12:10:00Z")
        )
    }
}
