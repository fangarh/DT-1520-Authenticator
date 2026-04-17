package ru.dt1520.security.authenticator.app

import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import ru.dt1520.security.authenticator.core.ui.theme.DT1520AuthenticatorTheme
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningRoute
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodesRoute
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.StoredTotpSecret

@Composable
fun AuthenticatorApp(
    secureStoreOverride: SecureTotpSecretStore? = null,
    currentEpochSecondsProvider: () -> Long = { System.currentTimeMillis() / 1_000 },
    clockTickDelayMillis: Long = 1_000L
) {
    val context = LocalContext.current
    val secureStore = secureStoreOverride ?: remember(context) {
        AndroidKeystoreSecureTotpSecretStore.create(context)
    }
    var storedSecrets by remember {
        mutableStateOf<List<StoredTotpSecret>>(emptyList())
    }
    var runtimeErrorMessage by remember {
        mutableStateOf<String?>(null)
    }
    var currentEpochSeconds by remember {
        mutableStateOf(currentEpochSecondsProvider())
    }

    suspend fun refreshStoredSecrets() {
        runCatching {
            loadStoredSecrets(secureStore)
        }.fold(
            onSuccess = { secrets ->
                storedSecrets = secrets
                runtimeErrorMessage = null
            },
            onFailure = {
                storedSecrets = emptyList()
                runtimeErrorMessage =
                    "Не удалось прочитать сохраненные TOTP-учетные записи. Перезапустите приложение и повторите попытку."
            }
        )
    }

    LaunchedEffect(secureStore) {
        refreshStoredSecrets()
    }

    LaunchedEffect(currentEpochSecondsProvider, clockTickDelayMillis) {
        while (true) {
            currentEpochSeconds = currentEpochSecondsProvider()
            delay(clockTickDelayMillis)
        }
    }

    DT1520AuthenticatorTheme {
        Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(innerPadding)
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                ProvisioningRoute(
                    onSaveImport = { preview ->
                        secureStore.save(preview.toStoredSecret())
                        refreshStoredSecrets()
                    }
                )

                runtimeErrorMessage?.let { message ->
                    Text(
                        text = message,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }

                TotpCodesRoute(
                    credentials = storedSecrets.toTotpCredentials(),
                    currentEpochSeconds = currentEpochSeconds,
                    onRemoveAccount = { account ->
                        secureStore.delete(account)
                        refreshStoredSecrets()
                    }
                )
            }
        }
    }
}
