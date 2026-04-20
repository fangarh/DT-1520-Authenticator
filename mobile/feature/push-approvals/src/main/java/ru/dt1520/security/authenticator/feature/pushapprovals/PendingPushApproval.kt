package ru.dt1520.security.authenticator.feature.pushapprovals

import java.time.Instant
import java.util.UUID

data class PendingPushApproval(
    val id: UUID,
    val operationType: String,
    val operationDisplayName: String? = null,
    val username: String? = null,
    val expiresAt: Instant,
    val correlationId: String? = null
) {
    init {
        require(operationType.isNotBlank()) {
            "operationType must not be blank."
        }
    }
}
