package ru.dt1520.security.authenticator.security.storage

import android.content.Context

class AndroidKeystoreSecureTotpSecretStore private constructor(
    private val delegate: SecureTotpSecretStore
) : SecureTotpSecretStore by delegate {
    companion object {
        private const val DEFAULT_PREFERENCES_NAME: String = "dt1520.totp.secrets"

        fun create(
            context: Context,
            preferencesName: String = DEFAULT_PREFERENCES_NAME,
            keyAlias: String = SecureTotpSecretRecord.DEFAULT_KEY_ALIAS
        ): SecureTotpSecretStore {
            val sharedPreferences = context.applicationContext.getSharedPreferences(
                preferencesName,
                Context.MODE_PRIVATE
            )

            return AndroidKeystoreSecureTotpSecretStore(
                delegate = SharedPreferencesSecureTotpSecretStore(
                    keyValueStore = SharedPreferencesTotpSecretKeyValueStore(sharedPreferences),
                    cipher = AndroidKeystoreTotpSecretCipher(keyAlias = keyAlias)
                )
            )
        }
    }
}
