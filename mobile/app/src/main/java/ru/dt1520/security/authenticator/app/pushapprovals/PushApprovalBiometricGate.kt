package ru.dt1520.security.authenticator.app.pushapprovals

import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval

internal interface PushApprovalBiometricGate {
    suspend fun verifyApproval(
        challenge: PendingPushApproval
    ): PushApprovalBiometricGateResult
}

internal sealed interface PushApprovalBiometricGateResult {
    data object Verified : PushApprovalBiometricGateResult

    data class Rejected(
        val userMessage: String
    ) : PushApprovalBiometricGateResult {
        init {
            require(userMessage.isNotBlank()) {
                "userMessage must not be blank."
            }
        }
    }
}
