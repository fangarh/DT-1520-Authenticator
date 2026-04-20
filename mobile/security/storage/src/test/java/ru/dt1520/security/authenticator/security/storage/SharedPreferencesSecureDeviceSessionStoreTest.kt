package ru.dt1520.security.authenticator.security.storage

import java.util.UUID
import kotlin.coroutines.Continuation
import kotlin.coroutines.EmptyCoroutineContext
import kotlin.coroutines.startCoroutine
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class SharedPreferencesSecureDeviceSessionStoreTest {
    @Test
    fun savesAndRestoresInstallationAndSession() {
        val store = SharedPreferencesSecureDeviceSessionStore(
            keyValueStore = InMemoryDeviceSessionKeyValueStore(),
            cipher = FakeDeviceSessionCipher()
        )
        val installation = StoredDeviceInstallation("installation-1234")
        val session = StoredDeviceSession(
            deviceId = UUID.fromString("e3140ae8-5fc2-4e9d-a6c2-b91b8fe57e79"),
            accessToken = "access-token",
            refreshToken = "refresh-token",
            tokenType = "Bearer",
            scope = "challenge",
            accessTokenExpiresAtEpochSeconds = 1_800_000_000
        )

        runSuspend {
            store.saveInstallation(installation)
            store.saveSession(session)
        }

        assertEquals(installation, runSuspend { store.readInstallation() })
        assertEquals(session, runSuspend { store.readSession() })
    }

    @Test
    fun clearSessionDoesNotDeleteInstallationIdentity() {
        val store = SharedPreferencesSecureDeviceSessionStore(
            keyValueStore = InMemoryDeviceSessionKeyValueStore(),
            cipher = FakeDeviceSessionCipher()
        )
        val installation = StoredDeviceInstallation("installation-keep")
        val session = StoredDeviceSession(
            deviceId = UUID.randomUUID(),
            accessToken = "access-token",
            refreshToken = "refresh-token",
            tokenType = "Bearer",
            scope = "challenge",
            accessTokenExpiresAtEpochSeconds = 1_800_000_000
        )

        runSuspend {
            store.saveInstallation(installation)
            store.saveSession(session)
            store.clearSession()
        }

        assertEquals(installation, runSuspend { store.readInstallation() })
        assertNull(runSuspend { store.readSession() })
    }

    @Test(expected = SecureDeviceSessionStorageException::class)
    fun failsClosedWhenSessionRecordIsCorrupted() {
        val keyValueStore = InMemoryDeviceSessionKeyValueStore()
        val store = SharedPreferencesSecureDeviceSessionStore(
            keyValueStore = keyValueStore,
            cipher = FakeDeviceSessionCipher()
        )

        keyValueStore.put(DeviceSessionStorageKeys.SESSION, "corrupted-value")

        runSuspend {
            store.readSession()
        }
    }

    @Test
    fun returnsNullWhenNothingIsStored() {
        val store = SharedPreferencesSecureDeviceSessionStore(
            keyValueStore = InMemoryDeviceSessionKeyValueStore(),
            cipher = FakeDeviceSessionCipher()
        )

        assertNull(runSuspend { store.readInstallation() })
        assertNull(runSuspend { store.readSession() })
    }

    @Test
    fun persistsEncryptedRecordsForBothKeys() {
        val keyValueStore = InMemoryDeviceSessionKeyValueStore()
        val store = SharedPreferencesSecureDeviceSessionStore(
            keyValueStore = keyValueStore,
            cipher = FakeDeviceSessionCipher()
        )

        runSuspend {
            store.saveInstallation(StoredDeviceInstallation("installation-visible-only-after-decrypt"))
            store.saveSession(
                StoredDeviceSession(
                    deviceId = UUID.randomUUID(),
                    accessToken = "access-token",
                    refreshToken = "refresh-token",
                    tokenType = "Bearer",
                    scope = "challenge",
                    accessTokenExpiresAtEpochSeconds = 1_800_000_000
                )
            )
        }

        assertNotNull(keyValueStore.get(DeviceSessionStorageKeys.INSTALLATION))
        assertNotNull(keyValueStore.get(DeviceSessionStorageKeys.SESSION))
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

private class InMemoryDeviceSessionKeyValueStore : DeviceSessionKeyValueStore {
    private val backingMap = linkedMapOf<String, String>()

    override fun get(key: String): String? = backingMap[key]

    override fun put(key: String, value: String) {
        backingMap[key] = value
    }

    override fun delete(key: String) {
        backingMap.remove(key)
    }
}

private class FakeDeviceSessionCipher : DeviceSessionCipher {
    override fun encrypt(plainText: String): SecureDeviceSessionRecord {
        return SecureDeviceSessionRecord(
            initializationVector = "iv-${plainText.length}",
            encryptedPayload = "cipher:${plainText.reversed()}",
            keyAlias = "fake.alias"
        )
    }

    override fun decrypt(record: SecureDeviceSessionRecord): String {
        require(record.encryptedPayload.startsWith("cipher:")) {
            "Unexpected encrypted payload format."
        }

        return record.encryptedPayload.removePrefix("cipher:").reversed()
    }
}
