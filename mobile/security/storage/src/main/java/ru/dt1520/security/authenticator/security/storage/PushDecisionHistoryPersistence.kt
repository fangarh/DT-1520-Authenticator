package ru.dt1520.security.authenticator.security.storage

import java.util.Base64

internal data class StoredPushDecisionHistorySnapshot(
    val entries: List<StoredPushDecisionHistoryEntry>
)

internal object StoredPushDecisionHistorySnapshotSerializer {
    fun serialize(snapshot: StoredPushDecisionHistorySnapshot): String {
        return snapshot.entries.joinToString(ENTRY_SEPARATOR) { entry ->
            listOf(
                SecurePushDecisionHistoryRecord.CURRENT_SCHEMA_VERSION.toString(),
                encode(entry.operationType),
                encodeNullable(entry.operationDisplayName),
                encodeNullable(entry.username),
                entry.decision.persistedValue,
                entry.decidedAtEpochSeconds.toString()
            ).joinToString(FIELD_SEPARATOR)
        }
    }

    fun deserialize(serialized: String): StoredPushDecisionHistorySnapshot {
        if (serialized.isBlank()) {
            return StoredPushDecisionHistorySnapshot(emptyList())
        }

        val entries = serialized.split(ENTRY_SEPARATOR)
            .map { serializedEntry -> deserializeEntry(serializedEntry) }

        return StoredPushDecisionHistorySnapshot(entries)
    }

    private fun deserializeEntry(serializedEntry: String): StoredPushDecisionHistoryEntry {
        val parts = serializedEntry.split(FIELD_SEPARATOR)
        require(parts.size == ENTRY_PARTS_COUNT) {
            "Stored push decision history entry has invalid format."
        }
        require(parts[0].toIntOrNull() == SecurePushDecisionHistoryRecord.CURRENT_SCHEMA_VERSION) {
            "Stored push decision history entry version is unsupported."
        }

        return StoredPushDecisionHistoryEntry(
            operationType = decode(parts[1]),
            operationDisplayName = decodeNullable(parts[2]),
            username = decodeNullable(parts[3]),
            decision = StoredPushDecisionHistoryDecision.fromPersisted(parts[4]),
            decidedAtEpochSeconds = parts[5].toLongOrNull()
                ?: throw IllegalArgumentException("Stored push decision timestamp is invalid.")
        )
    }
}

internal object SecurePushDecisionHistoryRecordSerializer {
    fun serialize(record: SecurePushDecisionHistoryRecord): String {
        return listOf(
            record.schemaVersion.toString(),
            encode(record.keyAlias),
            encode(record.initializationVector),
            encode(record.encryptedPayload)
        ).joinToString(FIELD_SEPARATOR)
    }

    fun deserialize(serialized: String): SecurePushDecisionHistoryRecord {
        val parts = serialized.split(FIELD_SEPARATOR)
        require(parts.size == RECORD_PARTS_COUNT) {
            "Stored push decision history record has invalid format."
        }

        return SecurePushDecisionHistoryRecord(
            schemaVersion = parts[0].toIntOrNull()
                ?: throw IllegalArgumentException("Stored push decision history record version is invalid."),
            keyAlias = decode(parts[1]),
            initializationVector = decode(parts[2]),
            encryptedPayload = decode(parts[3])
        )
    }
}

internal object PushDecisionHistoryStorageKeys {
    const val HISTORY: String = "push.decision.history"
}

private fun encode(value: String): String {
    return Base64.getUrlEncoder()
        .withoutPadding()
        .encodeToString(value.toByteArray(Charsets.UTF_8))
}

private fun encodeNullable(value: String?): String = value?.let(::encode) ?: NULL_SENTINEL

private fun decode(value: String): String {
    return String(Base64.getUrlDecoder().decode(value), Charsets.UTF_8)
}

private fun decodeNullable(value: String): String? {
    return if (value == NULL_SENTINEL) {
        null
    } else {
        decode(value)
    }
}

private const val FIELD_SEPARATOR: String = "|"
private const val ENTRY_SEPARATOR: String = "\n"
private const val NULL_SENTINEL: String = "~"
private const val ENTRY_PARTS_COUNT: Int = 6
private const val RECORD_PARTS_COUNT: Int = 4
