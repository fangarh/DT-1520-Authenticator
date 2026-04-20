package ru.dt1520.security.authenticator.feature.pushapprovals

data class PushApprovalSummary(
    val challenge: PendingPushApproval,
    val title: String,
    val supportingText: String,
    val expiresInSeconds: Long
) {
    val expiresInDisplay: String
        get() = when {
            expiresInSeconds >= 120L -> "Истекает через ${expiresInSeconds / 60L} мин"
            expiresInSeconds > 0L -> "Истекает через ${expiresInSeconds}с"
            else -> "Срок действия истекает"
        }

    val isExpiringSoon: Boolean
        get() = expiresInSeconds in 1..30
}
