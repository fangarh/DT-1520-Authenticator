package ru.dt1520.security.authenticator.security.storage

import kotlin.coroutines.Continuation
import kotlin.coroutines.EmptyCoroutineContext
import kotlin.coroutines.startCoroutine
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SharedPreferencesSecurePushDecisionHistoryStoreTest {
    @Test
    fun appendPersistsHistoryAndReturnsNewestEntriesFirst() {
        val store = SharedPreferencesSecurePushDecisionHistoryStore(
            keyValueStore = InMemoryPushDecisionHistoryKeyValueStore(),
            cipher = FakePushDecisionHistoryCipher()
        )

        runSuspend {
            store.append(
                StoredPushDecisionHistoryEntry(
                    operationType = "login",
                    operationDisplayName = "Console sign-in",
                    username = "operator@example.local",
                    decision = StoredPushDecisionHistoryDecision.Approved,
                    decidedAtEpochSeconds = 1_700_000_000L
                ),
                limit = 10
            )
            store.append(
                StoredPushDecisionHistoryEntry(
                    operationType = "step_up",
                    username = "operator@example.local",
                    decision = StoredPushDecisionHistoryDecision.Denied,
                    decidedAtEpochSeconds = 1_700_000_100L
                ),
                limit = 10
            )
        }

        val history = runSuspend { store.list(limit = 10) }

        assertEquals(2, history.size)
        assertEquals("step_up", history[0].operationType)
        assertEquals(StoredPushDecisionHistoryDecision.Denied, history[0].decision)
        assertEquals("login", history[1].operationType)
        assertEquals("Console sign-in", history[1].operationDisplayName)
    }

    @Test
    fun appendTrimsHistoryToConfiguredLimit() {
        val store = SharedPreferencesSecurePushDecisionHistoryStore(
            keyValueStore = InMemoryPushDecisionHistoryKeyValueStore(),
            cipher = FakePushDecisionHistoryCipher()
        )

        runSuspend {
            store.append(
                historyEntry(operationType = "login", decidedAtEpochSeconds = 1_700_000_000L),
                limit = 2
            )
            store.append(
                historyEntry(operationType = "step_up", decidedAtEpochSeconds = 1_700_000_100L),
                limit = 2
            )
            store.append(
                historyEntry(operationType = "device_activation", decidedAtEpochSeconds = 1_700_000_200L),
                limit = 2
            )
        }

        val history = runSuspend { store.list(limit = 10) }

        assertEquals(2, history.size)
        assertEquals("device_activation", history[0].operationType)
        assertEquals("step_up", history[1].operationType)
    }

    @Test(expected = SecurePushDecisionHistoryStorageException::class)
    fun failsClosedWhenHistoryRecordIsCorrupted() {
        val keyValueStore = InMemoryPushDecisionHistoryKeyValueStore().apply {
            put(PushDecisionHistoryStorageKeys.HISTORY, "corrupted-value")
        }
        val store = SharedPreferencesSecurePushDecisionHistoryStore(
            keyValueStore = keyValueStore,
            cipher = FakePushDecisionHistoryCipher()
        )

        runSuspend {
            store.list(limit = 10)
        }
    }

    @Test
    fun storesEncryptedPayloadInPreferences() {
        val keyValueStore = InMemoryPushDecisionHistoryKeyValueStore()
        val store = SharedPreferencesSecurePushDecisionHistoryStore(
            keyValueStore = keyValueStore,
            cipher = FakePushDecisionHistoryCipher()
        )

        runSuspend {
            store.append(
                historyEntry(operationType = "login", decidedAtEpochSeconds = 1_700_000_000L),
                limit = 10
            )
        }

        val persistedValue = keyValueStore.get(PushDecisionHistoryStorageKeys.HISTORY)

        assertNotNull(persistedValue)
        assertTrue(persistedValue?.isNotBlank() == true)
        assertTrue(persistedValue?.contains("operator@example.local") == false)
    }

    private fun historyEntry(
        operationType: String,
        decidedAtEpochSeconds: Long
    ): StoredPushDecisionHistoryEntry {
        return StoredPushDecisionHistoryEntry(
            operationType = operationType,
            username = "operator@example.local",
            decision = StoredPushDecisionHistoryDecision.Approved,
            decidedAtEpochSeconds = decidedAtEpochSeconds
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

private class InMemoryPushDecisionHistoryKeyValueStore : DeviceSessionKeyValueStore {
    private val backingMap = linkedMapOf<String, String>()

    override fun get(key: String): String? = backingMap[key]

    override fun put(key: String, value: String) {
        backingMap[key] = value
    }

    override fun delete(key: String) {
        backingMap.remove(key)
    }
}

private class FakePushDecisionHistoryCipher : PushDecisionHistoryCipher {
    override fun encrypt(plainText: String): SecurePushDecisionHistoryRecord {
        return SecurePushDecisionHistoryRecord(
            initializationVector = "iv-${plainText.length}",
            encryptedPayload = "cipher:${plainText.reversed()}",
            keyAlias = "fake.alias"
        )
    }

    override fun decrypt(record: SecurePushDecisionHistoryRecord): String {
        require(record.encryptedPayload.startsWith("cipher:")) {
            "Unexpected encrypted payload format."
        }

        return record.encryptedPayload.removePrefix("cipher:").reversed()
    }
}
