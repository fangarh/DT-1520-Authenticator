package ru.dt1520.security.authenticator.feature.pushapprovals

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import java.time.Instant
import kotlinx.coroutines.launch
import ru.dt1520.security.authenticator.core.ui.AppSection

@Composable
fun PushApprovalsRoute(
    pendingChallenges: List<PendingPushApproval>,
    currentEpochSeconds: Long,
    decisionHistory: List<PushDecisionHistoryEntry> = emptyList(),
    statusMessage: String? = null,
    onApproveChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult,
    onDenyChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult
) {
    var actionState by remember {
        mutableStateOf(PushApprovalActionState())
    }
    val coroutineScope = rememberCoroutineScope()
    val uiState = remember(pendingChallenges, currentEpochSeconds) {
        PushApprovalsPresenter.present(
            challenges = pendingChallenges,
            currentInstant = Instant.ofEpochSecond(currentEpochSeconds)
        )
    }

    AppSection(
        title = "Ожидающие push-запросы",
        description = "Подтверждение проходит только после локальной биометрии или device credential, а история решений хранится локально без transport-данных."
    ) {
        statusMessage?.let { message ->
            Text(
                text = message,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodyMedium
            )
        }

        actionState.errorMessage
            ?.takeIf { it != statusMessage }
            ?.let { message ->
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.testTag(PushApprovalsTestTags.ErrorMessage)
                )
            }

        Column(
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            if (uiState.isEmpty) {
                Text(
                    text = uiState.emptyMessage,
                    modifier = Modifier.testTag(PushApprovalsTestTags.EmptyStateMessage)
                )
            } else {
                uiState.summaries.forEach { summary ->
                    val isApproving = actionState.isApproving(summary.challenge.id)
                    val isDenying = actionState.isDenying(summary.challenge.id)
                    val buttonsEnabled = !actionState.hasDecisionInFlight

                    PushApprovalDecisionCard(
                        summary = summary,
                        buttonsEnabled = buttonsEnabled,
                        isApproving = isApproving,
                        isDenying = isDenying,
                        onApproveClick = {
                            actionState = PushApprovalDecisionWorkflow.clearError(actionState)
                            coroutineScope.launch {
                                val startedState = PushApprovalDecisionWorkflow.beginApprove(
                                    state = actionState,
                                    challengeId = summary.challenge.id
                                )
                                actionState = startedState

                                val nextState = runCatching {
                                    onApproveChallenge(summary.challenge)
                                }.fold(
                                    onSuccess = { result ->
                                        when (result) {
                                            PushApprovalDecisionResult.Success ->
                                                PushApprovalDecisionWorkflow.complete(
                                                    state = startedState,
                                                    challengeId = summary.challenge.id
                                                )

                                            is PushApprovalDecisionResult.Failure ->
                                                PushApprovalDecisionWorkflow.fail(
                                                    state = startedState,
                                                    challengeId = summary.challenge.id,
                                                    errorMessage = result.userMessage
                                                )
                                        }
                                    },
                                    onFailure = {
                                        PushApprovalDecisionWorkflow.fail(
                                            state = startedState,
                                            challengeId = summary.challenge.id
                                        )
                                    }
                                )

                                actionState = nextState
                            }
                        },
                        onDenyClick = {
                            actionState = PushApprovalDecisionWorkflow.clearError(actionState)
                            coroutineScope.launch {
                                val startedState = PushApprovalDecisionWorkflow.beginDeny(
                                    state = actionState,
                                    challengeId = summary.challenge.id
                                )
                                actionState = startedState

                                val nextState = runCatching {
                                    onDenyChallenge(summary.challenge)
                                }.fold(
                                    onSuccess = { result ->
                                        when (result) {
                                            PushApprovalDecisionResult.Success ->
                                                PushApprovalDecisionWorkflow.complete(
                                                    state = startedState,
                                                    challengeId = summary.challenge.id
                                                )

                                            is PushApprovalDecisionResult.Failure ->
                                                PushApprovalDecisionWorkflow.fail(
                                                    state = startedState,
                                                    challengeId = summary.challenge.id,
                                                    errorMessage = result.userMessage
                                                )
                                        }
                                    },
                                    onFailure = {
                                        PushApprovalDecisionWorkflow.fail(
                                            state = startedState,
                                            challengeId = summary.challenge.id
                                        )
                                    }
                                )

                                actionState = nextState
                            }
                        }
                    )
                }
            }

            HorizontalDivider()
            PushDecisionHistorySection(history = decisionHistory)
        }
    }
}
