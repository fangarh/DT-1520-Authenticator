package ru.dt1520.security.authenticator.totp.domain

class TotpCredential(
    val account: TotpAccountDescriptor,
    val secret: TotpSecret,
    val digits: Int = DEFAULT_DIGITS,
    val algorithm: TotpAlgorithm = TotpAlgorithm.Sha1
) {
    init {
        require(digits in 6..8) {
            "digits must be between 6 and 8."
        }
    }

    internal val codeModulus: Int
        get() = digitModulus[digits]

    override fun toString(): String =
        "TotpCredential(account=$account, digits=$digits, algorithm=$algorithm, secret=**redacted**)"

    companion object {
        const val DEFAULT_DIGITS = 6

        private val digitModulus = intArrayOf(
            1,
            10,
            100,
            1_000,
            10_000,
            100_000,
            1_000_000,
            10_000_000,
            100_000_000
        )
    }
}
