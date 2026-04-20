package ru.dt1520.security.authenticator.feature.pushapprovals

import java.time.Instant
import java.util.UUID
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class PushApprovalsPresenterTest {
    private val now = Instant.parse("2026-04-17T10:00:00Z")

    @Test
    fun returnsEmptyStateWhenNoActivePendingChallengesRemain() {
        val state = PushApprovalsPresenter.present(
            challenges = listOf(
                PendingPushApproval(
                    id = UUID.randomUUID(),
                    operationType = "login",
                    expiresAt = now.minusSeconds(5)
                )
            ),
            currentInstant = now
        )

        assertTrue(state.isEmpty)
        assertEquals(
            PushApprovalsUiState.DEFAULT_EMPTY_MESSAGE,
            state.emptyMessage
        )
    }

    @Test
    fun sortsChallengesByExpiryAndUsesOperationDisplayNameWhenProvided() {
        val stepUp = PendingPushApproval(
            id = UUID.randomUUID(),
            operationType = "step_up",
            operationDisplayName = "VPN access",
            username = "operator@example.local",
            expiresAt = now.plusSeconds(45)
        )
        val login = PendingPushApproval(
            id = UUID.randomUUID(),
            operationType = "login",
            username = "operator@example.local",
            expiresAt = now.plusSeconds(90)
        )

        val state = PushApprovalsPresenter.present(
            challenges = listOf(login, stepUp),
            currentInstant = now
        )

        assertEquals(2, state.summaries.size)
        assertEquals(stepUp.id, state.summaries[0].challenge.id)
        assertEquals("VPN access", state.summaries[0].title)
        assertEquals("Подтверждение входа", state.summaries[1].title)
        assertTrue(state.summaries[0].supportingText.contains("operator@example.local"))
    }
}
