package ru.dt1520.security.authenticator.security.storage

import java.util.UUID

data class StoredDeviceInstallation(
    val installationId: String
) {
    init {
        require(installationId.isNotBlank()) {
            "installationId must not be blank."
        }
    }
}

data class StoredDeviceSession(
    val deviceId: UUID,
    val accessToken: String,
    val refreshToken: String,
    val tokenType: String,
    val scope: String,
    val accessTokenExpiresAtEpochSeconds: Long
) {
    init {
        require(accessToken.isNotBlank()) {
            "accessToken must not be blank."
        }
        require(refreshToken.isNotBlank()) {
            "refreshToken must not be blank."
        }
        require(tokenType.isNotBlank()) {
            "tokenType must not be blank."
        }
        require(scope.isNotBlank()) {
            "scope must not be blank."
        }
        require(accessTokenExpiresAtEpochSeconds > 0) {
            "accessTokenExpiresAtEpochSeconds must be positive."
        }
    }
}

data class SecureDeviceSessionRecord(
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
        const val DEFAULT_KEY_ALIAS: String = "dt1520.device.session"
        const val CURRENT_SCHEMA_VERSION: Int = 1
    }
}

class SecureDeviceSessionStorageException(
    message: String,
    cause: Throwable? = null
) : IllegalStateException(message, cause)

interface SecureDeviceSessionStore {
    suspend fun readInstallation(): StoredDeviceInstallation?

    suspend fun saveInstallation(installation: StoredDeviceInstallation)

    suspend fun readSession(): StoredDeviceSession?

    suspend fun saveSession(session: StoredDeviceSession)

    suspend fun clearSession()
}
