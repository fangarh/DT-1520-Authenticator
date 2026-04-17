package ru.dt1520.security.authenticator.totp.domain

data class TotpCodeState(
    val code: String,
    val remainingSeconds: Int,
    val periodSeconds: Int,
    val validFromEpochSeconds: Long,
    val validUntilEpochSeconds: Long
) {
    init {
        require(code.isNotBlank() && code.all(Char::isDigit)) {
            "code must contain digits only."
        }
        require(periodSeconds > 0) {
            "periodSeconds must be positive."
        }
        require(remainingSeconds in 1..periodSeconds) {
            "remainingSeconds must fit current TOTP period."
        }
        require(validUntilEpochSeconds > validFromEpochSeconds) {
            "validUntilEpochSeconds must be greater than validFromEpochSeconds."
        }
    }

    val isExpiringSoon: Boolean
        get() = remainingSeconds <= 5
}
