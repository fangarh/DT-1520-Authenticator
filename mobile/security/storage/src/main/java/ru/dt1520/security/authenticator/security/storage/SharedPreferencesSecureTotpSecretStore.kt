package ru.dt1520.security.authenticator.security.storage

import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm

internal class SharedPreferencesSecureTotpSecretStore(
    private val keyValueStore: TotpSecretKeyValueStore,
    private val cipher: TotpSecretCipher
) : SecureTotpSecretStore {
    override suspend fun list(): List<TotpAccountDescriptor> {
        return keyValueStore.entries()
            .values
            .map(::restoreSecret)
            .map(StoredTotpSecret::account)
            .sortedWith(
                compareBy(
                    String.CASE_INSENSITIVE_ORDER,
                    TotpAccountDescriptor::displayName
                )
            )
    }

    override suspend fun read(account: TotpAccountDescriptor): StoredTotpSecret? {
        val serializedRecord = keyValueStore.entries()[SecureTotpSecretStorageKeyFactory.create(account)]
            ?: return null

        return restoreSecret(serializedRecord)
    }

    override suspend fun save(secret: StoredTotpSecret) {
        val snapshot = StoredTotpSecretSnapshot(
            issuer = secret.account.issuer,
            accountName = secret.account.accountName,
            periodSeconds = secret.account.periodSeconds,
            secret = secret.secret,
            digits = secret.digits,
            algorithm = secret.algorithm.parameterValue
        )
        val record = cipher.encrypt(
            plainText = StoredTotpSecretSnapshotSerializer.serialize(snapshot)
        )

        keyValueStore.put(
            key = SecureTotpSecretStorageKeyFactory.create(secret.account),
            value = SecureTotpSecretRecordSerializer.serialize(record)
        )
    }

    override suspend fun delete(account: TotpAccountDescriptor) {
        keyValueStore.delete(SecureTotpSecretStorageKeyFactory.create(account))
    }

    private fun restoreSecret(serializedRecord: String): StoredTotpSecret {
        val record = try {
            SecureTotpSecretRecordSerializer.deserialize(serializedRecord)
        } catch (exception: IllegalArgumentException) {
            throw SecureTotpSecretStorageException(
                message = "Stored TOTP secret record is corrupted.",
                cause = exception
            )
        }

        val snapshotPayload = cipher.decrypt(record)
        val snapshot = try {
            StoredTotpSecretSnapshotSerializer.deserialize(snapshotPayload)
        } catch (exception: IllegalArgumentException) {
            throw SecureTotpSecretStorageException(
                message = "Stored TOTP secret payload is corrupted.",
                cause = exception
            )
        }

        return StoredTotpSecret(
            account = TotpAccountDescriptor(
                issuer = snapshot.issuer,
                accountName = snapshot.accountName,
                periodSeconds = snapshot.periodSeconds
            ),
            secret = snapshot.secret,
            digits = snapshot.digits,
            algorithm = TotpAlgorithm.fromParameter(snapshot.algorithm)
        )
    }
}
