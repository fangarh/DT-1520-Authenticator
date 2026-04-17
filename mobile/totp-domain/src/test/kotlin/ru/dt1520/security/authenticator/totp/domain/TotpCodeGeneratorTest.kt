package ru.dt1520.security.authenticator.totp.domain

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class TotpCodeGeneratorTest {
    @Test
    fun generatesRfcVectorForSha1() {
        val credential = createCredential(
            secretAscii = "12345678901234567890",
            algorithm = TotpAlgorithm.Sha1
        )

        val state = TotpCodeGenerator.generate(
            credential = credential,
            epochSeconds = 59
        )

        assertEquals("94287082", state.code)
        assertEquals(1, state.remainingSeconds)
        assertTrue(state.isExpiringSoon)
    }

    @Test
    fun generatesRfcVectorForSha256() {
        val credential = createCredential(
            secretAscii = "12345678901234567890123456789012",
            algorithm = TotpAlgorithm.Sha256
        )

        val state = TotpCodeGenerator.generate(
            credential = credential,
            epochSeconds = 59
        )

        assertEquals("46119246", state.code)
    }

    @Test
    fun generatesRfcVectorForSha512() {
        val credential = createCredential(
            secretAscii = "1234567890123456789012345678901234567890123456789012345678901234",
            algorithm = TotpAlgorithm.Sha512
        )

        val state = TotpCodeGenerator.generate(
            credential = credential,
            epochSeconds = 59
        )

        assertEquals("90693936", state.code)
    }

    @Test
    fun keepsCodeStableInsideSingleTimeStep() {
        val credential = createCredential(
            secretAscii = "12345678901234567890"
        )

        val first = TotpCodeGenerator.generate(
            credential = credential,
            epochSeconds = 30
        )
        val second = TotpCodeGenerator.generate(
            credential = credential,
            epochSeconds = 54
        )

        assertEquals(first.code, second.code)
        assertEquals(30, first.remainingSeconds)
        assertEquals(6, second.remainingSeconds)
        assertFalse(second.isExpiringSoon)
    }

    private fun createCredential(
        secretAscii: String,
        algorithm: TotpAlgorithm = TotpAlgorithm.Sha1
    ): TotpCredential = TotpCredential(
        account = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local",
            periodSeconds = 30
        ),
        secret = TotpSecret.fromDecodedBytes(secretAscii.toByteArray(Charsets.US_ASCII)),
        digits = 8,
        algorithm = algorithm
    )
}
