package ru.dt1520.security.authenticator.totp.domain

object TotpCodeGenerator {
    fun generate(
        credential: TotpCredential,
        epochSeconds: Long
    ): TotpCodeState {
        val periodSeconds = credential.account.periodSeconds.toLong()
        val movingFactor = Math.floorDiv(epochSeconds, periodSeconds)
        val validFromEpochSeconds = movingFactor * periodSeconds
        val validUntilEpochSeconds = validFromEpochSeconds + periodSeconds
        val remainingSeconds = (validUntilEpochSeconds - epochSeconds).toInt()
        val code = generateHotp(
            credential = credential,
            movingFactor = movingFactor
        )

        return TotpCodeState(
            code = code,
            remainingSeconds = remainingSeconds,
            periodSeconds = credential.account.periodSeconds,
            validFromEpochSeconds = validFromEpochSeconds,
            validUntilEpochSeconds = validUntilEpochSeconds
        )
    }

    private fun generateHotp(
        credential: TotpCredential,
        movingFactor: Long
    ): String {
        val hash = credential.algorithm.createMac(credential.secret)
            .doFinal(movingFactor.toByteArray())
        val offset = hash.last().toInt() and 0x0F
        val binaryCode = ((hash[offset].toInt() and 0x7F) shl 24) or
            ((hash[offset + 1].toInt() and 0xFF) shl 16) or
            ((hash[offset + 2].toInt() and 0xFF) shl 8) or
            (hash[offset + 3].toInt() and 0xFF)
        val otpValue = binaryCode % credential.codeModulus

        return otpValue.toString().padStart(credential.digits, '0')
    }

    private fun Long.toByteArray(): ByteArray {
        val buffer = ByteArray(8)

        for (index in buffer.indices.reversed()) {
            buffer[index] = ((this ushr ((7 - index) * 8)) and 0xFF).toByte()
        }

        return buffer
    }
}
