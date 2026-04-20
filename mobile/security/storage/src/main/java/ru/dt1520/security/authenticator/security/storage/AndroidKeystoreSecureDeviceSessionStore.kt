package ru.dt1520.security.authenticator.security.storage

import android.content.Context

class AndroidKeystoreSecureDeviceSessionStore private constructor(
    private val delegate: SecureDeviceSessionStore
) : SecureDeviceSessionStore by delegate {
    companion object {
        private const val DEFAULT_PREFERENCES_NAME: String = "dt1520.device.session"

        fun create(
            context: Context,
            preferencesName: String = DEFAULT_PREFERENCES_NAME,
            keyAlias: String = SecureDeviceSessionRecord.DEFAULT_KEY_ALIAS
        ): SecureDeviceSessionStore {
            val sharedPreferences = context.applicationContext.getSharedPreferences(
                preferencesName,
                Context.MODE_PRIVATE
            )

            return AndroidKeystoreSecureDeviceSessionStore(
                delegate = SharedPreferencesSecureDeviceSessionStore(
                    keyValueStore = SharedPreferencesDeviceSessionKeyValueStore(sharedPreferences),
                    cipher = AndroidKeystoreDeviceSessionCipher(keyAlias = keyAlias)
                )
            )
        }
    }
}
