package ru.dt1520.security.authenticator.app

import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onFirst
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.time.Instant
import java.util.UUID
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.dt1520.security.authenticator.app.pushapprovals.PushApprovalDecisionCoordinator
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalsTestTags
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryDecision
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryEntry

@RunWith(AndroidJUnit4::class)
class PushApprovalsUiTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun approveMovesChallengeIntoLocalHistory() {
        val coordinator = RecordingPushApprovalDecisionCoordinator()
        val challenge = sampleChallenge()

        composeRule.setContent {
            AuthenticatorApp(
                pushApprovalDecisionCoordinatorOverride = coordinator,
                pendingPushApprovals = listOf(challenge),
                currentEpochSecondsProvider = { 1_765_736_400L },
                clockTickDelayMillis = 60_000L
            )
        }

        composeRule.onAllNodesWithTag(PushApprovalsTestTags.ApproveButton).onFirst()
            .performClick()

        composeRule.waitUntil(timeoutMillis = 5_000L) {
            coordinator.approvedChallenges.size == 1
        }
        composeRule.waitUntil(timeoutMillis = 5_000L) {
            composeRule.onAllNodesWithTag(PushApprovalsTestTags.HistoryEntry)
                .fetchSemanticsNodes().isNotEmpty()
        }

        composeRule.onNodeWithText("Подтверждено локально", substring = true).assertExists()
        composeRule.onNodeWithText("Пользователь: operator@example.local").assertExists()
        composeRule.onNodeWithTag(PushApprovalsTestTags.EmptyStateMessage).assertExists()
    }

    @Test
    fun biometricFailureUsesSafeMessageWithoutWritingHistory() {
        val coordinator = RecordingPushApprovalDecisionCoordinator(
            approveResult = PushApprovalDecisionResult.Failure(
                userMessage = "Локальная биометрия была отменена."
            )
        )

        composeRule.setContent {
            AuthenticatorApp(
                pushApprovalDecisionCoordinatorOverride = coordinator,
                pendingPushApprovals = listOf(sampleChallenge()),
                currentEpochSecondsProvider = { 1_765_736_400L },
                clockTickDelayMillis = 60_000L
            )
        }

        composeRule.onAllNodesWithTag(PushApprovalsTestTags.ApproveButton).onFirst()
            .performClick()

        composeRule.waitUntil(timeoutMillis = 5_000L) {
            composeRule.onAllNodesWithTag(PushApprovalsTestTags.ErrorMessage)
                .fetchSemanticsNodes().isNotEmpty()
        }

        composeRule.onNodeWithText("Локальная биометрия была отменена.").assertExists()
        composeRule.onNodeWithTag(PushApprovalsTestTags.HistoryEmptyState).assertExists()
    }

    private class RecordingPushApprovalDecisionCoordinator(
        private val approveResult: PushApprovalDecisionResult = PushApprovalDecisionResult.Success
    ) : PushApprovalDecisionCoordinator {
        private val history = mutableListOf<PushDecisionHistoryEntry>()
        val approvedChallenges = mutableListOf<UUID>()

        override suspend fun listDecisionHistory(): List<PushDecisionHistoryEntry> = history.toList()

        override suspend fun approve(challenge: PendingPushApproval): PushApprovalDecisionResult {
            if (approveResult == PushApprovalDecisionResult.Success) {
                approvedChallenges += challenge.id
                history.add(
                    0,
                    PushDecisionHistoryEntry(
                        operationType = challenge.operationType,
                        operationDisplayName = challenge.operationDisplayName,
                        username = challenge.username,
                        decision = PushDecisionHistoryDecision.Approved,
                        decidedAt = Instant.parse("2026-04-17T12:00:00Z")
                    )
                )
            }

            return approveResult
        }

        override suspend fun deny(challenge: PendingPushApproval): PushApprovalDecisionResult {
            history.add(
                0,
                PushDecisionHistoryEntry(
                    operationType = challenge.operationType,
                    operationDisplayName = challenge.operationDisplayName,
                    username = challenge.username,
                    decision = PushDecisionHistoryDecision.Denied,
                    decidedAt = Instant.parse("2026-04-17T12:00:00Z")
                )
            )

            return PushApprovalDecisionResult.Success
        }
    }

    private fun sampleChallenge(): PendingPushApproval {
        return PendingPushApproval(
            id = UUID.fromString("5cfa0ab1-6aa6-4c5c-a95d-544eb3c4774e"),
            operationType = "login",
            username = "operator@example.local",
            expiresAt = Instant.parse("2026-04-17T12:10:00Z")
        )
    }
}
