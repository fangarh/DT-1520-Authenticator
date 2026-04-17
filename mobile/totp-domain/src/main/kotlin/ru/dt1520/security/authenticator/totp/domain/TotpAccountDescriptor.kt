package ru.dt1520.security.authenticator.totp.domain

data class TotpAccountDescriptor(
    val issuer: String,
    val accountName: String,
    val periodSeconds: Int = 30
) {
    init {
        require(issuer.isNotBlank()) {
            "issuer must not be blank."
        }
        require(accountName.isNotBlank()) {
            "accountName must not be blank."
        }
        require(periodSeconds > 0) {
            "periodSeconds must be positive."
        }
    }

    val displayName: String
        get() = if (issuer.equals(accountName, ignoreCase = true)) {
            issuer
        } else {
            "$issuer ($accountName)"
        }

    val canonicalLabel: String
        get() = "$issuer:$accountName"
}
