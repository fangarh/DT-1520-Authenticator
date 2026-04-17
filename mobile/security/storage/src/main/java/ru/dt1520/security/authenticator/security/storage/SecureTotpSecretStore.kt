package ru.dt1520.security.authenticator.security.storage

import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm
import ru.dt1520.security.authenticator.totp.domain.TotpCredential
import ru.dt1520.security.authenticator.totp.domain.TotpSecret

data class StoredTotpSecret(
    val account: TotpAccountDescriptor,
    val secret: String,
    val digits: Int = TotpCredential.DEFAULT_DIGITS,
    val algorithm: TotpAlgorithm = TotpAlgorithm.Sha1
) {
    init {
        require(secret.isNotBlank()) {
            "secret must not be blank."
        }
        require(digits in 6..8) {
            "digits must be between 6 and 8."
        }
    }

    fun toCredential(): TotpCredential = TotpCredential(
        account = account,
        secret = TotpSecret.fromBase32(secret),
        digits = digits,
        algorithm = algorithm
    )
}

data class SecureTotpSecretRecord(
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
        const val DEFAULT_KEY_ALIAS: String = "dt1520.totp.secret"
        const val CURRENT_SCHEMA_VERSION: Int = 1
    }
}

class SecureTotpSecretStorageException(
    message: String,
    cause: Throwable? = null
) : IllegalStateException(message, cause)

interface SecureTotpSecretStore {
    suspend fun list(): List<TotpAccountDescriptor>

    suspend fun read(account: TotpAccountDescriptor): StoredTotpSecret?

    suspend fun save(secret: StoredTotpSecret)

    suspend fun delete(account: TotpAccountDescriptor)
}
