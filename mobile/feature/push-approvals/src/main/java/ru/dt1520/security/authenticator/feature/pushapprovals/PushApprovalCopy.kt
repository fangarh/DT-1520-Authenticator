package ru.dt1520.security.authenticator.feature.pushapprovals

internal object PushApprovalCopy {
    fun defaultTitle(operationType: String): String {
        return when (operationType) {
            "login" -> "Подтверждение входа"
            "step_up" -> "Дополнительная проверка"
            "backup_code_recovery" -> "Восстановление backup codes"
            "device_activation" -> "Активация устройства"
            "totp_enrollment" -> "Подтверждение TOTP enrollment"
            else -> "Подтверждение операции"
        }
    }

    fun historyDecisionLabel(decision: PushDecisionHistoryDecision): String {
        return when (decision) {
            PushDecisionHistoryDecision.Approved -> "Подтверждено локально"
            PushDecisionHistoryDecision.Denied -> "Отклонено на устройстве"
        }
    }
}
