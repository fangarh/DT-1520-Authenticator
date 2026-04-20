package ru.dt1520.security.authenticator.security.storage

internal class SharedPreferencesSecureDeviceSessionStore(
    private val keyValueStore: DeviceSessionKeyValueStore,
    private val cipher: DeviceSessionCipher
) : SecureDeviceSessionStore {
    override suspend fun readInstallation(): StoredDeviceInstallation? {
        val serializedRecord = keyValueStore.get(DeviceSessionStorageKeys.INSTALLATION)
            ?: return null

        return restoreInstallation(serializedRecord)
    }

    override suspend fun saveInstallation(installation: StoredDeviceInstallation) {
        val record = cipher.encrypt(
            plainText = StoredDeviceInstallationSnapshotSerializer.serialize(installation.toSnapshot())
        )
        keyValueStore.put(
            key = DeviceSessionStorageKeys.INSTALLATION,
            value = SecureDeviceSessionRecordSerializer.serialize(record)
        )
    }

    override suspend fun readSession(): StoredDeviceSession? {
        val serializedRecord = keyValueStore.get(DeviceSessionStorageKeys.SESSION)
            ?: return null

        return restoreSession(serializedRecord)
    }

    override suspend fun saveSession(session: StoredDeviceSession) {
        val record = cipher.encrypt(
            plainText = StoredDeviceSessionSnapshotSerializer.serialize(session.toSnapshot())
        )
        keyValueStore.put(
            key = DeviceSessionStorageKeys.SESSION,
            value = SecureDeviceSessionRecordSerializer.serialize(record)
        )
    }

    override suspend fun clearSession() {
        keyValueStore.delete(DeviceSessionStorageKeys.SESSION)
    }

    private fun restoreInstallation(serializedRecord: String): StoredDeviceInstallation {
        val record = deserializeRecord(serializedRecord)
        val snapshotPayload = cipher.decrypt(record)
        val snapshot = try {
            StoredDeviceInstallationSnapshotSerializer.deserialize(snapshotPayload)
        } catch (exception: IllegalArgumentException) {
            throw SecureDeviceSessionStorageException(
                message = "Stored device installation payload is corrupted.",
                cause = exception
            )
        }

        return snapshot.toInstallation()
    }

    private fun restoreSession(serializedRecord: String): StoredDeviceSession {
        val record = deserializeRecord(serializedRecord)
        val snapshotPayload = cipher.decrypt(record)
        val snapshot = try {
            StoredDeviceSessionSnapshotSerializer.deserialize(snapshotPayload)
        } catch (exception: IllegalArgumentException) {
            throw SecureDeviceSessionStorageException(
                message = "Stored device session payload is corrupted.",
                cause = exception
            )
        }

        return try {
            snapshot.toSession()
        } catch (exception: IllegalArgumentException) {
            throw SecureDeviceSessionStorageException(
                message = "Stored device session payload is invalid.",
                cause = exception
            )
        }
    }

    private fun deserializeRecord(serializedRecord: String): SecureDeviceSessionRecord {
        return try {
            SecureDeviceSessionRecordSerializer.deserialize(serializedRecord)
        } catch (exception: IllegalArgumentException) {
            throw SecureDeviceSessionStorageException(
                message = "Stored device session record is corrupted.",
                cause = exception
            )
        }
    }
}
