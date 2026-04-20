package ru.dt1520.security.authenticator.feature.pushapprovals

import java.util.UUID
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PushApprovalDecisionWorkflowTest {
    private val challengeId = UUID.randomUUID()

    @Test
    fun beginApproveMarksApproveDecisionInFlight() {
        val nextState = PushApprovalDecisionWorkflow.beginApprove(
            state = PushApprovalActionState(),
            challengeId = challengeId
        )

        assertTrue(nextState.activeDecision is PushApprovalDecision.Approve)
        assertTrue(nextState.isApproving(challengeId))
        assertNull(nextState.errorMessage)
    }

    @Test
    fun beginDenyMarksDenyDecisionInFlight() {
        val nextState = PushApprovalDecisionWorkflow.beginDeny(
            state = PushApprovalActionState(),
            challengeId = challengeId
        )

        assertTrue(nextState.activeDecision is PushApprovalDecision.Deny)
        assertTrue(nextState.isDenying(challengeId))
        assertNull(nextState.errorMessage)
    }

    @Test
    fun completeClearsMatchingDecision() {
        val startedState = PushApprovalDecisionWorkflow.beginApprove(
            state = PushApprovalActionState(),
            challengeId = challengeId
        )

        val finishedState = PushApprovalDecisionWorkflow.complete(
            state = startedState,
            challengeId = challengeId
        )

        assertNull(finishedState.activeDecision)
        assertNull(finishedState.errorMessage)
    }

    @Test
    fun failMapsDecisionIntoGenericSafeMessage() {
        val startedState = PushApprovalDecisionWorkflow.beginDeny(
            state = PushApprovalActionState(),
            challengeId = challengeId
        )

        val failedState = PushApprovalDecisionWorkflow.fail(
            state = startedState,
            challengeId = challengeId
        )

        assertNull(failedState.activeDecision)
        assertEquals(
            "Не удалось отклонить запрос на этом устройстве. Повторите попытку после обновления соединения.",
            failedState.errorMessage
        )
    }

    @Test
    fun failUsesCustomSafeMessageWhenProvided() {
        val startedState = PushApprovalDecisionWorkflow.beginApprove(
            state = PushApprovalActionState(),
            challengeId = challengeId
        )

        val failedState = PushApprovalDecisionWorkflow.fail(
            state = startedState,
            challengeId = challengeId,
            errorMessage = "Локальная биометрия была отменена."
        )

        assertEquals("Локальная биометрия была отменена.", failedState.errorMessage)
    }
}
