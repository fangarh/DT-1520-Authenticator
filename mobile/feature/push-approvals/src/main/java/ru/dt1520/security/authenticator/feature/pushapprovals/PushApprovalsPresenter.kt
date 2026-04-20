package ru.dt1520.security.authenticator.feature.pushapprovals

import java.time.Instant

object PushApprovalsPresenter {
    fun present(
        challenges: List<PendingPushApproval>,
        currentInstant: Instant
    ): PushApprovalsUiState {
        val summaries = challenges
            .asSequence()
            .filter { it.expiresAt.isAfter(currentInstant) }
            .sortedWith(
                compareBy<PendingPushApproval> { it.expiresAt }
                    .thenBy { it.operationDisplayName ?: it.operationType }
            )
            .map { challenge ->
                PushApprovalSummary(
                    challenge = challenge,
                    title = challenge.operationDisplayName?.takeIf { it.isNotBlank() }
                        ?: PushApprovalCopy.defaultTitle(challenge.operationType),
                    supportingText = buildSupportingText(challenge),
                    expiresInSeconds = challenge.expiresAt.epochSecond - currentInstant.epochSecond
                )
            }
            .toList()

        return PushApprovalsUiState(summaries = summaries)
    }

    private fun buildSupportingText(challenge: PendingPushApproval): String {
        val parts = buildList {
            challenge.username?.takeIf { it.isNotBlank() }?.let { add("Пользователь: $it") }
            challenge.correlationId?.takeIf { it.isNotBlank() }?.let { add("Correlation: $it") }
        }

        return if (parts.isEmpty()) {
            "Запрос уже привязан к текущему устройству."
        } else {
            parts.joinToString(separator = " • ")
        }
    }
}
