package ru.dt1520.security.authenticator.totp.domain

class TotpSecret private constructor(
    private val keyMaterial: ByteArray
) {
    init {
        require(keyMaterial.isNotEmpty()) {
            "TOTP secret must not be empty."
        }
    }

    internal fun copyKeyMaterial(): ByteArray = keyMaterial.copyOf()

    fun toBase32(): String = encodeBase32(keyMaterial)

    override fun equals(other: Any?): Boolean {
        if (this === other) {
            return true
        }
        if (other !is TotpSecret) {
            return false
        }

        return keyMaterial.contentEquals(other.keyMaterial)
    }

    override fun hashCode(): Int = keyMaterial.contentHashCode()

    override fun toString(): String = "TotpSecret(**redacted**)"

    companion object {
        private val alphabetLookup = IntArray(128) { -1 }.apply {
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".forEachIndexed { index, character ->
                this[character.code] = index
            }
        }

        fun fromBase32(encoded: String): TotpSecret {
            val normalized = normalizeBase32(encoded)
            return TotpSecret(decodeBase32(normalized))
        }

        internal fun fromDecodedBytes(keyMaterial: ByteArray): TotpSecret =
            TotpSecret(keyMaterial.copyOf())

        private fun normalizeBase32(encoded: String): String {
            require(encoded.isNotBlank()) {
                "TOTP secret must not be empty."
            }

            val normalized = StringBuilder(encoded.length)
            var sawPadding = false

            encoded.forEach { character ->
                when {
                    character == '=' -> sawPadding = true
                    character.isWhitespace() || character == '-' -> Unit
                    sawPadding -> throw IllegalArgumentException("Invalid Base32 secret.")
                    character.code >= alphabetLookup.size -> throw IllegalArgumentException("Invalid Base32 secret.")
                    else -> {
                        normalized.append(character.uppercaseChar())
                    }
                }
            }

            require(normalized.isNotEmpty()) {
                "TOTP secret must not be empty."
            }

            return normalized.toString()
        }

        private fun decodeBase32(normalized: String): ByteArray {
            val output = ByteArray((normalized.length * 5) / 8)
            var buffer = 0
            var bitsInBuffer = 0
            var outputIndex = 0

            normalized.forEach { character ->
                val alphabetIndex = alphabetLookup.getOrNull(character.code)
                    ?: throw IllegalArgumentException("Invalid Base32 secret.")

                require(alphabetIndex >= 0) {
                    "Invalid Base32 secret."
                }

                buffer = (buffer shl 5) or alphabetIndex
                bitsInBuffer += 5

                if (bitsInBuffer >= 8) {
                    bitsInBuffer -= 8
                    output[outputIndex] = ((buffer shr bitsInBuffer) and 0xFF).toByte()
                    outputIndex += 1
                }
            }

            require(outputIndex > 0) {
                "TOTP secret must not be empty."
            }

            return output
        }

        private fun encodeBase32(keyMaterial: ByteArray): String {
            val output = StringBuilder(((keyMaterial.size + 4) / 5) * 8)
            var buffer = 0
            var bitsInBuffer = 0

            keyMaterial.forEach { byte ->
                buffer = (buffer shl 8) or (byte.toInt() and 0xFF)
                bitsInBuffer += 8

                while (bitsInBuffer >= 5) {
                    bitsInBuffer -= 5
                    output.append(base32Alphabet[(buffer shr bitsInBuffer) and 0x1F])
                }
            }

            if (bitsInBuffer > 0) {
                output.append(base32Alphabet[(buffer shl (5 - bitsInBuffer)) and 0x1F])
            }

            return output.toString()
        }

        private val base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".toCharArray()
    }
}
