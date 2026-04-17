package ru.dt1520.security.authenticator.feature.totpcodes

data class TotpCodesUiState(
    val summaries: List<TotpCodeSummary>,
    val emptyMessage: String = DEFAULT_EMPTY_MESSAGE
) {
    val isEmpty: Boolean
        get() = summaries.isEmpty()

    companion object {
        const val DEFAULT_EMPTY_MESSAGE: String =
            "Сохраните первый TOTP secret через Provisioning, чтобы увидеть офлайн-коды."
    }
}
