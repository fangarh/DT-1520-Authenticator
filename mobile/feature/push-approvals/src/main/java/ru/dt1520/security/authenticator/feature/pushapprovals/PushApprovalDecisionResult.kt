package ru.dt1520.security.authenticator.feature.pushapprovals

sealed interface PushApprovalDecisionResult {
    data object Success : PushApprovalDecisionResult

    data class Failure(
        val userMessage: String,
        val statusMessage: String? = null,
        val shouldClearPendingChallenges: Boolean = false
    ) : PushApprovalDecisionResult {
        init {
            require(userMessage.isNotBlank()) {
                "userMessage must not be blank."
            }
        }
    }
}
