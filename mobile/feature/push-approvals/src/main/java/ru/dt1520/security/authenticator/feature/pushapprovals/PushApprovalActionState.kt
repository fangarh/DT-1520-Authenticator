package ru.dt1520.security.authenticator.feature.pushapprovals

import java.util.UUID

data class PushApprovalActionState(
    val activeDecision: PushApprovalDecision? = null,
    val errorMessage: String? = null
) {
    fun isApproving(challengeId: UUID): Boolean =
        activeDecision is PushApprovalDecision.Approve &&
            activeDecision.challengeId == challengeId

    fun isDenying(challengeId: UUID): Boolean =
        activeDecision is PushApprovalDecision.Deny &&
            activeDecision.challengeId == challengeId

    val hasDecisionInFlight: Boolean
        get() = activeDecision != null
}

sealed interface PushApprovalDecision {
    val challengeId: UUID

    data class Approve(
        override val challengeId: UUID
    ) : PushApprovalDecision

    data class Deny(
        override val challengeId: UUID
    ) : PushApprovalDecision
}
