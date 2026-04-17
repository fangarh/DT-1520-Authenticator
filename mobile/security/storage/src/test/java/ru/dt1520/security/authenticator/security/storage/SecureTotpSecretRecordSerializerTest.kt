package ru.dt1520.security.authenticator.security.storage

import org.junit.Assert.assertEquals
import org.junit.Test

class SecureTotpSecretRecordSerializerTest {
    @Test
    fun roundTripsSerializedRecord() {
        val record = SecureTotpSecretRecord(
            initializationVector = "iv-data",
            encryptedPayload = "ciphertext-data",
            keyAlias = "dt1520.alias",
            schemaVersion = 3
        )

        val restored = SecureTotpSecretRecordSerializer.deserialize(
            SecureTotpSecretRecordSerializer.serialize(record)
        )

        assertEquals(record, restored)
    }

    @Test
    fun roundTripsSerializedSnapshot() {
        val snapshot = StoredTotpSecretSnapshot(
            issuer = "DT 1520",
            accountName = "operator@example.local",
            periodSeconds = 30,
            secret = "JBSWY3DPEHPK3PXP",
            digits = 8,
            algorithm = "SHA256"
        )

        val restored = StoredTotpSecretSnapshotSerializer.deserialize(
            StoredTotpSecretSnapshotSerializer.serialize(snapshot)
        )

        assertEquals(snapshot, restored)
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsMalformedSerializedRecord() {
        SecureTotpSecretRecordSerializer.deserialize("broken-record")
    }
}
