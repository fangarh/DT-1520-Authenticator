package ru.dt1520.security.authenticator.feature.totpcodes

import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor

data class TotpCodeSummary(
    val account: TotpAccountDescriptor,
    val code: String,
    val remainingSeconds: Int
) {
    init {
        require(code.isNotBlank() && code.all(Char::isDigit)) {
            "code must contain digits only."
        }
        require(remainingSeconds in 0..account.periodSeconds) {
            "remainingSeconds must fit current TOTP period."
        }
    }

    val isExpiringSoon: Boolean
        get() = remainingSeconds <= 5

    val formattedCode: String
        get() = code.chunked(size = when (code.length) {
            8 -> 4
            else -> 3
        }).joinToString(separator = " ")
}
