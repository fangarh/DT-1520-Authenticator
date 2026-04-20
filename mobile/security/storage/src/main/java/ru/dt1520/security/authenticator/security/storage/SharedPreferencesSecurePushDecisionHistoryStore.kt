package ru.dt1520.security.authenticator.security.storage

internal class SharedPreferencesSecurePushDecisionHistoryStore(
    private val keyValueStore: DeviceSessionKeyValueStore,
    private val cipher: PushDecisionHistoryCipher
) : SecurePushDecisionHistoryStore {
    override suspend fun list(limit: Int): List<StoredPushDecisionHistoryEntry> {
        require(limit > 0) {
            "limit must be positive."
        }

        val serializedRecord = keyValueStore.get(PushDecisionHistoryStorageKeys.HISTORY)
            ?: return emptyList()

        val record = deserializeRecord(serializedRecord)
        val snapshotPayload = cipher.decrypt(record)
        val snapshot = try {
            StoredPushDecisionHistorySnapshotSerializer.deserialize(snapshotPayload)
        } catch (exception: IllegalArgumentException) {
            throw SecurePushDecisionHistoryStorageException(
                message = "Stored push decision history payload is corrupted.",
                cause = exception
            )
        }

        return snapshot.entries.take(limit)
    }

    override suspend fun append(
        entry: StoredPushDecisionHistoryEntry,
        limit: Int
    ) {
        require(limit > 0) {
            "limit must be positive."
        }

        val updatedEntries = listOf(entry) + list(limit)
        val trimmedEntries = updatedEntries.take(limit)
        val serializedSnapshot = StoredPushDecisionHistorySnapshotSerializer.serialize(
            StoredPushDecisionHistorySnapshot(trimmedEntries)
        )
        val record = cipher.encrypt(serializedSnapshot)

        keyValueStore.put(
            key = PushDecisionHistoryStorageKeys.HISTORY,
            value = SecurePushDecisionHistoryRecordSerializer.serialize(record)
        )
    }

    private fun deserializeRecord(serializedRecord: String): SecurePushDecisionHistoryRecord {
        return try {
            SecurePushDecisionHistoryRecordSerializer.deserialize(serializedRecord)
        } catch (exception: IllegalArgumentException) {
            throw SecurePushDecisionHistoryStorageException(
                message = "Stored push decision history record is corrupted.",
                cause = exception
            )
        }
    }
}
