package ru.dt1520.security.authenticator.security.storage

import kotlin.coroutines.Continuation
import kotlin.coroutines.EmptyCoroutineContext
import kotlin.coroutines.startCoroutine
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm

class SharedPreferencesSecureTotpSecretStoreTest {
    @Test
    fun savesAndRestoresSecretPayload() {
        val keyValueStore = InMemoryTotpSecretKeyValueStore()
        val store = SharedPreferencesSecureTotpSecretStore(
            keyValueStore = keyValueStore,
            cipher = FakeTotpSecretCipher()
        )
        val account = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local"
        )

        runSuspend {
            store.save(
                StoredTotpSecret(
                    account = account,
                    secret = "JBSWY3DPEHPK3PXP",
                    digits = 8,
                    algorithm = TotpAlgorithm.Sha256
                )
            )
        }

        val restored = runSuspend { store.read(account) }

        assertEquals(
            StoredTotpSecret(
                account = account,
                secret = "JBSWY3DPEHPK3PXP",
                digits = 8,
                algorithm = TotpAlgorithm.Sha256
            ),
            restored
        )
        val persistedKey = keyValueStore.entries().keys.single()
        assertFalse(persistedKey.contains(account.issuer))
        assertFalse(persistedKey.contains(account.accountName))
    }

    @Test
    fun listsAccountsFromRestoredSnapshots() {
        val store = SharedPreferencesSecureTotpSecretStore(
            keyValueStore = InMemoryTotpSecretKeyValueStore(),
            cipher = FakeTotpSecretCipher()
        )
        val secondAccount = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local"
        )
        val firstAccount = TotpAccountDescriptor(
            issuer = "Admin",
            accountName = "backup@example.local"
        )

        runSuspend {
            store.save(
                StoredTotpSecret(
                    account = secondAccount,
                    secret = "SECONDSECRET",
                    digits = 8,
                    algorithm = TotpAlgorithm.Sha512
                )
            )
            store.save(
                StoredTotpSecret(
                    account = firstAccount,
                    secret = "FIRSTSECRET",
                    digits = 6,
                    algorithm = TotpAlgorithm.Sha1
                )
            )
        }

        val accounts = runSuspend { store.list() }

        assertEquals(listOf(firstAccount, secondAccount), accounts)
    }

    @Test
    fun deletesStoredAccount() {
        val store = SharedPreferencesSecureTotpSecretStore(
            keyValueStore = InMemoryTotpSecretKeyValueStore(),
            cipher = FakeTotpSecretCipher()
        )
        val account = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local"
        )

        runSuspend {
            store.save(
                StoredTotpSecret(
                    account = account,
                    secret = "JBSWY3DPEHPK3PXP",
                    digits = 6,
                    algorithm = TotpAlgorithm.Sha1
                )
            )
            store.delete(account)
        }

        assertNull(runSuspend { store.read(account) })
        assertTrue(runSuspend { store.list() }.isEmpty())
    }

    @Test(expected = SecureTotpSecretStorageException::class)
    fun failsClosedWhenPersistedRecordIsCorrupted() {
        val keyValueStore = InMemoryTotpSecretKeyValueStore()
        val store = SharedPreferencesSecureTotpSecretStore(
            keyValueStore = keyValueStore,
            cipher = FakeTotpSecretCipher()
        )
        val account = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local"
        )

        keyValueStore.put(
            key = SecureTotpSecretStorageKeyFactory.create(account),
            value = "corrupted-value"
        )

        runSuspend { store.read(account) }
    }

    @Test
    fun returnsNullForUnknownAccount() {
        val store = SharedPreferencesSecureTotpSecretStore(
            keyValueStore = InMemoryTotpSecretKeyValueStore(),
            cipher = FakeTotpSecretCipher()
        )

        assertNull(
            runSuspend {
                store.read(
                    TotpAccountDescriptor(
                        issuer = "DT 1520",
                        accountName = "unknown@example.local"
                    )
                )
            }
        )
    }

    private fun <T> runSuspend(block: suspend () -> T): T {
        var outcome: Result<T>? = null

        block.startCoroutine(
            object : Continuation<T> {
                override val context = EmptyCoroutineContext

                override fun resumeWith(result: Result<T>) {
                    outcome = result
                }
            }
        )

        return outcome!!.getOrThrow()
    }
}

private class InMemoryTotpSecretKeyValueStore : TotpSecretKeyValueStore {
    private val backingMap = linkedMapOf<String, String>()

    override fun entries(): Map<String, String> = backingMap.toMap()

    override fun put(key: String, value: String) {
        backingMap[key] = value
    }

    override fun delete(key: String) {
        backingMap.remove(key)
    }
}

private class FakeTotpSecretCipher : TotpSecretCipher {
    override fun encrypt(plainText: String): SecureTotpSecretRecord {
        return SecureTotpSecretRecord(
            initializationVector = "iv-${plainText.length}",
            encryptedPayload = "cipher:${plainText.reversed()}",
            keyAlias = "fake.alias"
        )
    }

    override fun decrypt(record: SecureTotpSecretRecord): String {
        require(record.encryptedPayload.startsWith("cipher:")) {
            "Unexpected encrypted payload format."
        }

        return record.encryptedPayload.removePrefix("cipher:").reversed()
    }
}
