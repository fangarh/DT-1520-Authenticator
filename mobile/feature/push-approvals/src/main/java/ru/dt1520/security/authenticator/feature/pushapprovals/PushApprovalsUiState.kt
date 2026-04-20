package ru.dt1520.security.authenticator.feature.pushapprovals

data class PushApprovalsUiState(
    val summaries: List<PushApprovalSummary>,
    val emptyMessage: String = DEFAULT_EMPTY_MESSAGE
) {
    val isEmpty: Boolean
        get() = summaries.isEmpty()

    companion object {
        const val DEFAULT_EMPTY_MESSAGE =
            "На этом устройстве пока нет ожидающих push-запросов."
    }
}
