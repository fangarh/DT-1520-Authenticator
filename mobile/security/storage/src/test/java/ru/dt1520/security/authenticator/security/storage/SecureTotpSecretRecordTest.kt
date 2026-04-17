package ru.dt1520.security.authenticator.security.storage

import org.junit.Assert.assertEquals
import org.junit.Test
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm

class SecureTotpSecretRecordTest {
    @Test(expected = IllegalArgumentException::class)
    fun rejectsBlankInitializationVector() {
        SecureTotpSecretRecord(
            initializationVector = "",
            encryptedPayload = "ciphertext"
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsBlankEncryptedPayload() {
        SecureTotpSecretRecord(
            initializationVector = "iv",
            encryptedPayload = ""
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsBlankStoredSecret() {
        StoredTotpSecret(
            account = TotpAccountDescriptor(
                issuer = "DT 1520",
                accountName = "operator@example.local"
            ),
            secret = ""
        )
    }

    @Test
    fun keepsDefaultRecordParametersForBootstrapStorage() {
        val record = SecureTotpSecretRecord(
            initializationVector = "iv",
            encryptedPayload = "ciphertext"
        )

        assertEquals("dt1520.totp.secret", record.keyAlias)
        assertEquals(1, record.schemaVersion)
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsUnsupportedDigits() {
        StoredTotpSecret(
            account = TotpAccountDescriptor(
                issuer = "DT 1520",
                accountName = "operator@example.local"
            ),
            secret = "JBSWY3DPEHPK3PXP",
            digits = 5,
            algorithm = TotpAlgorithm.Sha256
        )
    }
}
