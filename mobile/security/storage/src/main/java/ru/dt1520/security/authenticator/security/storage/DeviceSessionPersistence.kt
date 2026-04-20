package ru.dt1520.security.authenticator.security.storage

import android.content.SharedPreferences
import java.util.Base64
import java.util.UUID

internal data class StoredDeviceInstallationSnapshot(
    val installationId: String
) {
    init {
        require(installationId.isNotBlank()) {
            "installationId must not be blank."
        }
    }
}

internal data class StoredDeviceSessionSnapshot(
    val deviceId: String,
    val accessToken: String,
    val refreshToken: String,
    val tokenType: String,
    val scope: String,
    val accessTokenExpiresAtEpochSeconds: Long
) {
    init {
        require(deviceId.isNotBlank()) {
            "deviceId must not be blank."
        }
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

internal interface DeviceSessionKeyValueStore {
    fun get(key: String): String?

    fun put(key: String, value: String)

    fun delete(key: String)
}

internal class SharedPreferencesDeviceSessionKeyValueStore(
    private val sharedPreferences: SharedPreferences
) : DeviceSessionKeyValueStore {
    override fun get(key: String): String? = sharedPreferences.getString(key, null)

    override fun put(key: String, value: String) {
        sharedPreferences.edit()
            .putString(key, value)
            .apply()
    }

    override fun delete(key: String) {
        sharedPreferences.edit()
            .remove(key)
            .apply()
    }
}

internal object StoredDeviceInstallationSnapshotSerializer {
    fun serialize(snapshot: StoredDeviceInstallationSnapshot): String {
        return listOf(
            SecureDeviceSessionRecord.CURRENT_SCHEMA_VERSION.toString(),
            encode(snapshot.installationId)
        ).joinToString(SEPARATOR)
    }

    fun deserialize(serialized: String): StoredDeviceInstallationSnapshot {
        val parts = serialized.split(SEPARATOR)
        require(parts.size == INSTALLATION_PARTS_COUNT) {
            "Stored device installation snapshot has invalid format."
        }
        require(parts[0].toIntOrNull() == SecureDeviceSessionRecord.CURRENT_SCHEMA_VERSION) {
            "Stored device installation snapshot version is unsupported."
        }

        return StoredDeviceInstallationSnapshot(
            installationId = decode(parts[1])
        )
    }
}

internal object StoredDeviceSessionSnapshotSerializer {
    fun serialize(snapshot: StoredDeviceSessionSnapshot): String {
        return listOf(
            SecureDeviceSessionRecord.CURRENT_SCHEMA_VERSION.toString(),
            encode(snapshot.deviceId),
            encode(snapshot.accessToken),
            encode(snapshot.refreshToken),
            encode(snapshot.tokenType),
            encode(snapshot.scope),
            snapshot.accessTokenExpiresAtEpochSeconds.toString()
        ).joinToString(SEPARATOR)
    }

    fun deserialize(serialized: String): StoredDeviceSessionSnapshot {
        val parts = serialized.split(SEPARATOR)
        require(parts.size == SESSION_PARTS_COUNT) {
            "Stored device session snapshot has invalid format."
        }
        require(parts[0].toIntOrNull() == SecureDeviceSessionRecord.CURRENT_SCHEMA_VERSION) {
            "Stored device session snapshot version is unsupported."
        }

        return StoredDeviceSessionSnapshot(
            deviceId = decode(parts[1]),
            accessToken = decode(parts[2]),
            refreshToken = decode(parts[3]),
            tokenType = decode(parts[4]),
            scope = decode(parts[5]),
            accessTokenExpiresAtEpochSeconds = parts[6].toLongOrNull()
                ?: throw IllegalArgumentException("Stored device session expiry is invalid.")
        )
    }
}

internal object SecureDeviceSessionRecordSerializer {
    fun serialize(record: SecureDeviceSessionRecord): String {
        return listOf(
            record.schemaVersion.toString(),
            encode(record.keyAlias),
            encode(record.initializationVector),
            encode(record.encryptedPayload)
        ).joinToString(SEPARATOR)
    }

    fun deserialize(serialized: String): SecureDeviceSessionRecord {
        val parts = serialized.split(SEPARATOR)
        require(parts.size == RECORD_PARTS_COUNT) {
            "Stored device session record has invalid format."
        }

        return SecureDeviceSessionRecord(
            schemaVersion = parts[0].toIntOrNull()
                ?: throw IllegalArgumentException("Stored device session record version is invalid."),
            keyAlias = decode(parts[1]),
            initializationVector = decode(parts[2]),
            encryptedPayload = decode(parts[3])
        )
    }
}

internal object DeviceSessionStorageKeys {
    const val INSTALLATION: String = "device.installation"
    const val SESSION: String = "device.session"
}

internal fun StoredDeviceInstallationSnapshot.toInstallation(): StoredDeviceInstallation = StoredDeviceInstallation(
    installationId = installationId
)

internal fun StoredDeviceSessionSnapshot.toSession(): StoredDeviceSession = StoredDeviceSession(
    deviceId = UUID.fromString(deviceId),
    accessToken = accessToken,
    refreshToken = refreshToken,
    tokenType = tokenType,
    scope = scope,
    accessTokenExpiresAtEpochSeconds = accessTokenExpiresAtEpochSeconds
)

internal fun StoredDeviceInstallation.toSnapshot(): StoredDeviceInstallationSnapshot = StoredDeviceInstallationSnapshot(
    installationId = installationId
)

internal fun StoredDeviceSession.toSnapshot(): StoredDeviceSessionSnapshot = StoredDeviceSessionSnapshot(
    deviceId = deviceId.toString(),
    accessToken = accessToken,
    refreshToken = refreshToken,
    tokenType = tokenType,
    scope = scope,
    accessTokenExpiresAtEpochSeconds = accessTokenExpiresAtEpochSeconds
)

private fun encode(value: String): String {
    return Base64.getUrlEncoder()
        .withoutPadding()
        .encodeToString(value.toByteArray(Charsets.UTF_8))
}

private fun decode(value: String): String {
    return String(Base64.getUrlDecoder().decode(value), Charsets.UTF_8)
}

private const val SEPARATOR: String = "|"
private const val INSTALLATION_PARTS_COUNT: Int = 2
private const val SESSION_PARTS_COUNT: Int = 7
private const val RECORD_PARTS_COUNT: Int = 4
