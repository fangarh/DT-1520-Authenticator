package ru.dt1520.security.authenticator.security.storage

import android.content.SharedPreferences
import java.security.MessageDigest
import java.util.Base64
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor

internal data class StoredTotpSecretSnapshot(
    val issuer: String,
    val accountName: String,
    val periodSeconds: Int,
    val secret: String,
    val digits: Int,
    val algorithm: String
) {
    init {
        require(issuer.isNotBlank()) {
            "issuer must not be blank."
        }
        require(accountName.isNotBlank()) {
            "accountName must not be blank."
        }
        require(periodSeconds > 0) {
            "periodSeconds must be positive."
        }
        require(secret.isNotBlank()) {
            "secret must not be blank."
        }
        require(digits in 6..8) {
            "digits must be between 6 and 8."
        }
        require(algorithm.isNotBlank()) {
            "algorithm must not be blank."
        }
    }
}

internal interface TotpSecretKeyValueStore {
    fun entries(): Map<String, String>

    fun put(key: String, value: String)

    fun delete(key: String)
}

internal class SharedPreferencesTotpSecretKeyValueStore(
    private val sharedPreferences: SharedPreferences
) : TotpSecretKeyValueStore {
    override fun entries(): Map<String, String> {
        return buildMap {
            for ((key, value) in sharedPreferences.all) {
                val stringValue = value as? String ?: continue
                put(key, stringValue)
            }
        }
    }

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

internal object StoredTotpSecretSnapshotSerializer {
    fun serialize(snapshot: StoredTotpSecretSnapshot): String {
        return listOf(
            SecureTotpSecretRecord.CURRENT_SCHEMA_VERSION.toString(),
            encode(snapshot.issuer),
            encode(snapshot.accountName),
            snapshot.periodSeconds.toString(),
            encode(snapshot.secret),
            snapshot.digits.toString(),
            encode(snapshot.algorithm)
        ).joinToString(SEPARATOR)
    }

    fun deserialize(serialized: String): StoredTotpSecretSnapshot {
        val parts = serialized.split(SEPARATOR)
        require(parts.size == SNAPSHOT_PARTS_COUNT) {
            "Stored TOTP secret snapshot has invalid format."
        }
        require(parts[0].toIntOrNull() == SecureTotpSecretRecord.CURRENT_SCHEMA_VERSION) {
            "Stored TOTP secret snapshot version is unsupported."
        }

        return StoredTotpSecretSnapshot(
            issuer = decode(parts[1]),
            accountName = decode(parts[2]),
            periodSeconds = parts[3].toIntOrNull()
                ?: throw IllegalArgumentException("Stored TOTP secret period is invalid."),
            secret = decode(parts[4]),
            digits = parts[5].toIntOrNull()
                ?: throw IllegalArgumentException("Stored TOTP secret digits are invalid."),
            algorithm = decode(parts[6])
        )
    }

    private fun encode(value: String): String {
        return Base64.getUrlEncoder()
            .withoutPadding()
            .encodeToString(value.toByteArray(Charsets.UTF_8))
    }

    private fun decode(value: String): String {
        return String(Base64.getUrlDecoder().decode(value), Charsets.UTF_8)
    }

    private const val SEPARATOR: String = "|"
    private const val SNAPSHOT_PARTS_COUNT: Int = 7
}

internal object SecureTotpSecretRecordSerializer {
    fun serialize(record: SecureTotpSecretRecord): String {
        return listOf(
            record.schemaVersion.toString(),
            encode(record.keyAlias),
            encode(record.initializationVector),
            encode(record.encryptedPayload)
        ).joinToString(SEPARATOR)
    }

    fun deserialize(serialized: String): SecureTotpSecretRecord {
        val parts = serialized.split(SEPARATOR)
        require(parts.size == RECORD_PARTS_COUNT) {
            "Stored TOTP secret record has invalid format."
        }

        return SecureTotpSecretRecord(
            schemaVersion = parts[0].toIntOrNull()
                ?: throw IllegalArgumentException("Stored TOTP secret record version is invalid."),
            keyAlias = decode(parts[1]),
            initializationVector = decode(parts[2]),
            encryptedPayload = decode(parts[3])
        )
    }

    private fun encode(value: String): String {
        return Base64.getUrlEncoder()
            .withoutPadding()
            .encodeToString(value.toByteArray(Charsets.UTF_8))
    }

    private fun decode(value: String): String {
        return String(Base64.getUrlDecoder().decode(value), Charsets.UTF_8)
    }

    private const val SEPARATOR: String = "|"
    private const val RECORD_PARTS_COUNT: Int = 4
}

internal object SecureTotpSecretStorageKeyFactory {
    fun create(account: TotpAccountDescriptor): String {
        val material = buildString {
            append(account.issuer)
            append('\n')
            append(account.accountName)
            append('\n')
            append(account.periodSeconds)
        }
        val digest = MessageDigest.getInstance(SHA_256)
            .digest(material.toByteArray(Charsets.UTF_8))

        return KEY_PREFIX + Base64.getUrlEncoder().withoutPadding().encodeToString(digest)
    }

    private const val KEY_PREFIX: String = "totp."
    private const val SHA_256: String = "SHA-256"
}
