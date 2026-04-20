package ru.dt1520.security.authenticator.security.storage

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.GeneralSecurityException
import java.security.KeyStore
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

internal interface DeviceSessionCipher {
    fun encrypt(plainText: String): SecureDeviceSessionRecord

    fun decrypt(record: SecureDeviceSessionRecord): String
}

internal class AndroidKeystoreDeviceSessionCipher(
    private val keyAlias: String = SecureDeviceSessionRecord.DEFAULT_KEY_ALIAS
) : DeviceSessionCipher {
    private val keyStore: KeyStore by lazy {
        KeyStore.getInstance(ANDROID_KEYSTORE).apply {
            load(null)
        }
    }

    override fun encrypt(plainText: String): SecureDeviceSessionRecord {
        return runCipherOperation("encrypt") {
            val cipher = Cipher.getInstance(AES_GCM_NO_PADDING)
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateSecretKey(keyAlias))

            SecureDeviceSessionRecord(
                initializationVector = Base64.getEncoder().encodeToString(cipher.iv),
                encryptedPayload = Base64.getEncoder().encodeToString(
                    cipher.doFinal(plainText.toByteArray(Charsets.UTF_8))
                ),
                keyAlias = keyAlias
            )
        }
    }

    override fun decrypt(record: SecureDeviceSessionRecord): String {
        return runCipherOperation("decrypt") {
            val cipher = Cipher.getInstance(AES_GCM_NO_PADDING)
            val ivBytes = Base64.getDecoder().decode(record.initializationVector)
            val payloadBytes = Base64.getDecoder().decode(record.encryptedPayload)

            cipher.init(
                Cipher.DECRYPT_MODE,
                getOrCreateSecretKey(record.keyAlias),
                GCMParameterSpec(GCM_TAG_LENGTH_BITS, ivBytes)
            )

            String(cipher.doFinal(payloadBytes), Charsets.UTF_8)
        }
    }

    private fun getOrCreateSecretKey(alias: String): SecretKey {
        val existingKey = keyStore.getKey(alias, null) as? SecretKey
        if (existingKey != null) {
            return existingKey
        }

        val generator = KeyGenerator.getInstance(
            KeyProperties.KEY_ALGORITHM_AES,
            ANDROID_KEYSTORE
        )
        generator.init(
            KeyGenParameterSpec.Builder(
                alias,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
            )
                .setKeySize(AES_KEY_SIZE_BITS)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build()
        )

        return generator.generateKey()
    }

    private fun <T> runCipherOperation(
        operation: String,
        block: () -> T
    ): T {
        try {
            return block()
        } catch (exception: IllegalArgumentException) {
            throw SecureDeviceSessionStorageException(
                message = "Unable to $operation device session payload.",
                cause = exception
            )
        } catch (exception: GeneralSecurityException) {
            throw SecureDeviceSessionStorageException(
                message = "Unable to $operation device session payload.",
                cause = exception
            )
        }
    }

    private companion object {
        const val AES_GCM_NO_PADDING: String = "AES/GCM/NoPadding"
        const val AES_KEY_SIZE_BITS: Int = 256
        const val ANDROID_KEYSTORE: String = "AndroidKeyStore"
        const val GCM_TAG_LENGTH_BITS: Int = 128
    }
}
