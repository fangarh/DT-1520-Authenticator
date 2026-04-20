package ru.dt1520.security.authenticator.feature.pushapprovals

import java.time.Instant

data class PushDecisionHistoryEntry(
    val operationType: String,
    val operationDisplayName: String? = null,
    val username: String? = null,
    val decision: PushDecisionHistoryDecision,
    val decidedAt: Instant
) {
    init {
        require(operationType.isNotBlank()) {
            "operationType must not be blank."
        }
    }
}

sealed interface PushDecisionHistoryDecision {
    data object Approved : PushDecisionHistoryDecision

    data object Denied : PushDecisionHistoryDecision
}
