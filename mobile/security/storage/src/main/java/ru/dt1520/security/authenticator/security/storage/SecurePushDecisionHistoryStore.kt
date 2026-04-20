package ru.dt1520.security.authenticator.security.storage

data class StoredPushDecisionHistoryEntry(
    val operationType: String,
    val operationDisplayName: String? = null,
    val username: String? = null,
    val decision: StoredPushDecisionHistoryDecision,
    val decidedAtEpochSeconds: Long
) {
    init {
        require(operationType.isNotBlank()) {
            "operationType must not be blank."
        }
        require(decidedAtEpochSeconds > 0L) {
            "decidedAtEpochSeconds must be positive."
        }
    }
}

sealed interface StoredPushDecisionHistoryDecision {
    val persistedValue: String

    data object Approved : StoredPushDecisionHistoryDecision {
        override val persistedValue: String = "approved"
    }

    data object Denied : StoredPushDecisionHistoryDecision {
        override val persistedValue: String = "denied"
    }

    companion object {
        fun fromPersisted(value: String): StoredPushDecisionHistoryDecision {
            return when (value) {
                Approved.persistedValue -> Approved
                Denied.persistedValue -> Denied
                else -> throw IllegalArgumentException("Stored push decision value is unsupported.")
            }
        }
    }
}

data class SecurePushDecisionHistoryRecord(
    val initializationVector: String,
    val encryptedPayload: String,
    val keyAlias: String = DEFAULT_KEY_ALIAS,
    val schemaVersion: Int = CURRENT_SCHEMA_VERSION
) {
    init {
        require(initializationVector.isNotBlank()) {
            "initializationVector must not be blank."
        }
        require(encryptedPayload.isNotBlank()) {
            "encryptedPayload must not be blank."
        }
        require(keyAlias.isNotBlank()) {
            "keyAlias must not be blank."
        }
        require(schemaVersion > 0) {
            "schemaVersion must be positive."
        }
    }

    companion object {
        const val DEFAULT_KEY_ALIAS: String = "dt1520.push.decision.history"
        const val CURRENT_SCHEMA_VERSION: Int = 1
    }
}

class SecurePushDecisionHistoryStorageException(
    message: String,
    cause: Throwable? = null
) : IllegalStateException(message, cause)

interface SecurePushDecisionHistoryStore {
    suspend fun list(limit: Int = DEFAULT_HISTORY_LIMIT): List<StoredPushDecisionHistoryEntry>

    suspend fun append(
        entry: StoredPushDecisionHistoryEntry,
        limit: Int = DEFAULT_HISTORY_LIMIT
    )

    companion object {
        const val DEFAULT_HISTORY_LIMIT: Int = 10
    }
}
