package ru.dt1520.security.authenticator.security.storage

import android.content.Context

class AndroidKeystoreSecurePushDecisionHistoryStore private constructor(
    private val delegate: SecurePushDecisionHistoryStore
) : SecurePushDecisionHistoryStore by delegate {
    companion object {
        private const val DEFAULT_PREFERENCES_NAME: String = "dt1520.push.decision.history"

        fun create(
            context: Context,
            preferencesName: String = DEFAULT_PREFERENCES_NAME,
            keyAlias: String = SecurePushDecisionHistoryRecord.DEFAULT_KEY_ALIAS
        ): SecurePushDecisionHistoryStore {
            val sharedPreferences = context.applicationContext.getSharedPreferences(
                preferencesName,
                Context.MODE_PRIVATE
            )

            return AndroidKeystoreSecurePushDecisionHistoryStore(
                delegate = SharedPreferencesSecurePushDecisionHistoryStore(
                    keyValueStore = SharedPreferencesDeviceSessionKeyValueStore(sharedPreferences),
                    cipher = AndroidKeystorePushDecisionHistoryCipher(keyAlias = keyAlias)
                )
            )
        }
    }
}
