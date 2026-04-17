package ru.dt1520.security.authenticator.totp.domain

import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

sealed class TotpAlgorithm(
    val parameterValue: String,
    private val macAlgorithmName: String
) {
    data object Sha1 : TotpAlgorithm(
        parameterValue = "SHA1",
        macAlgorithmName = "HmacSHA1"
    )

    data object Sha256 : TotpAlgorithm(
        parameterValue = "SHA256",
        macAlgorithmName = "HmacSHA256"
    )

    data object Sha512 : TotpAlgorithm(
        parameterValue = "SHA512",
        macAlgorithmName = "HmacSHA512"
    )

    internal fun createMac(secret: TotpSecret): Mac {
        val keyBytes = secret.copyKeyMaterial()

        return try {
            Mac.getInstance(macAlgorithmName).apply {
                init(SecretKeySpec(keyBytes, macAlgorithmName))
            }
        } finally {
            keyBytes.fill(0)
        }
    }

    override fun toString(): String = parameterValue

    companion object {
        fun fromParameter(value: String?): TotpAlgorithm {
            if (value == null) {
                return Sha1
            }

            return when (value.trim().uppercase()) {
                "SHA1" -> Sha1
                "SHA256" -> Sha256
                "SHA512" -> Sha512
                else -> throw IllegalArgumentException("Unsupported TOTP algorithm.")
            }
        }
    }
}
