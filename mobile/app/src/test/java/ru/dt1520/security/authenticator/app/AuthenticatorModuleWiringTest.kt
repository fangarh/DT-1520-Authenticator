package ru.dt1520.security.authenticator.app

import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningDraft
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStorageException
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.StoredTotpSecret
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor

class AuthenticatorModuleWiringTest {
    @Test
    fun provisioningPreviewMapsIntoStoredSecretWithoutLosingCredentialMetadata() {
        val draft = ProvisioningDraft(
            manualIssuer = "DT 1520",
            manualAccountName = "operator@example.local",
            manualSecret = "JBSWY3DPEHPK3PXP"
        )
        val preview = draft.buildManualPreview()
        val storedSecret = preview.toStoredSecret()

        assertTrue(draft.canPreviewManualImport)
        assertEquals("DT 1520 (operator@example.local)", preview.summary)
        assertEquals(TotpAlgorithm.Sha1, storedSecret.algorithm)
        assertEquals(preview.credential.account, storedSecret.toCredential().account)
        assertEquals(preview.credential.digits, storedSecret.toCredential().digits)
        assertEquals(preview.credential.algorithm, storedSecret.toCredential().algorithm)
        assertEquals(preview.credential.secret, storedSecret.toCredential().secret)
    }

    @Test
    fun loadStoredSecretsReturnsSortedCatalogFromSecureStore() = runBlocking {
        val alphaSecret = StoredTotpSecret(
            account = TotpAccountDescriptor(
                issuer = "Alpha",
                accountName = "operator@example.local"
            ),
            secret = "JBSWY3DPEHPK3PXP"
        )
        val betaSecret = StoredTotpSecret(
            account = TotpAccountDescriptor(
                issuer = "Beta",
                accountName = "operator@example.local"
            ),
            secret = "JBSWY3DPEHPK3PXP"
        )
        val store = FakeSecureTotpSecretStore(
            secrets = linkedMapOf(
                betaSecret.account to betaSecret,
                alphaSecret.account to alphaSecret
            )
        )

        val storedSecrets = loadStoredSecrets(store)

        assertEquals(
            listOf(alphaSecret.account, betaSecret.account),
            storedSecrets.map { it.account }
        )
        assertEquals(
            listOf(alphaSecret.account, betaSecret.account),
            storedSecrets.toTotpCredentials().map { it.account }
        )
    }

    @Test
    fun loadStoredSecretsFailsClosedWhenCatalogContainsMissingEntry() {
        val missingAccount = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "missing@example.local"
        )
        val store = FakeSecureTotpSecretStore(
            secrets = linkedMapOf(),
            listedAccounts = listOf(missingAccount)
        )

        assertThrows(SecureTotpSecretStorageException::class.java) {
            runBlocking {
                loadStoredSecrets(store)
            }
        }
    }

    private class FakeSecureTotpSecretStore(
        private val secrets: LinkedHashMap<TotpAccountDescriptor, StoredTotpSecret>,
        private val listedAccounts: List<TotpAccountDescriptor> = secrets.keys.toList()
    ) : SecureTotpSecretStore {
        override suspend fun list(): List<TotpAccountDescriptor> = listedAccounts

        override suspend fun read(account: TotpAccountDescriptor): StoredTotpSecret? = secrets[account]

        override suspend fun save(secret: StoredTotpSecret) {
            secrets[secret.account] = secret
        }

        override suspend fun delete(account: TotpAccountDescriptor) {
            secrets.remove(account)
        }
    }
}
