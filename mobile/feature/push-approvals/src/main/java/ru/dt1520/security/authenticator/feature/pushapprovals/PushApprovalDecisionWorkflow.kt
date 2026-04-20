package ru.dt1520.security.authenticator.feature.pushapprovals

import java.util.UUID

object PushApprovalDecisionWorkflow {
    fun beginApprove(
        state: PushApprovalActionState,
        challengeId: UUID
    ): PushApprovalActionState = state.copy(
        activeDecision = PushApprovalDecision.Approve(challengeId),
        errorMessage = null
    )

    fun beginDeny(
        state: PushApprovalActionState,
        challengeId: UUID
    ): PushApprovalActionState = state.copy(
        activeDecision = PushApprovalDecision.Deny(challengeId),
        errorMessage = null
    )

    fun complete(
        state: PushApprovalActionState,
        challengeId: UUID
    ): PushApprovalActionState {
        if (state.activeDecision?.challengeId != challengeId) {
            return state
        }

        return state.copy(
            activeDecision = null,
            errorMessage = null
        )
    }

    fun fail(
        state: PushApprovalActionState,
        challengeId: UUID,
        errorMessage: String? = null
    ): PushApprovalActionState {
        if (state.activeDecision?.challengeId != challengeId) {
            return state
        }

        val safeErrorMessage = errorMessage?.takeIf { it.isNotBlank() } ?: when (state.activeDecision) {
            is PushApprovalDecision.Approve ->
                "Не удалось подтвердить запрос на этом устройстве. Повторите попытку после обновления соединения."

            is PushApprovalDecision.Deny ->
                "Не удалось отклонить запрос на этом устройстве. Повторите попытку после обновления соединения."
        }

        return state.copy(
            activeDecision = null,
            errorMessage = safeErrorMessage
        )
    }

    fun clearError(state: PushApprovalActionState): PushApprovalActionState = state.copy(
        errorMessage = null
    )
}
