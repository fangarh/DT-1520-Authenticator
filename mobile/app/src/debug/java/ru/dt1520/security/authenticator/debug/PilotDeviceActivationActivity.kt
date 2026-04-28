package ru.dt1520.security.authenticator.debug

import android.os.Bundle
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.lifecycle.lifecycleScope
import java.util.UUID
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager
import ru.dt1520.security.authenticator.app.deviceruntime.HttpDeviceRuntimeTransport
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureDeviceSessionStore

class PilotDeviceActivationActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val status = TextView(this).apply {
            text = "Activating pilot device..."
            textSize = 18f
            setPadding(32, 64, 32, 32)
        }
        setContentView(status)

        lifecycleScope.launch {
            status.text = runCatching {
                when (optionalExtra("mode")) {
                    "pending" -> listPendingFromIntent()
                    else -> "Pilot device activated: ${activateFromIntent()}"
                }
            }.fold(
                onSuccess = { result -> result },
                onFailure = { exception -> "Pilot helper failed: ${exception.message}" }
            )
        }
    }

    private suspend fun activateFromIntent(): UUID = withContext(Dispatchers.IO) {
        val baseUrl = requiredExtra("baseUrl")
        val tenantId = UUID.fromString(requiredExtra("tenantId"))
        val externalUserId = requiredExtra("externalUserId")
        val activationCode = requiredExtra("activationCode")
        val integrationAccessToken = requiredExtra("integrationAccessToken")
        val deviceName = optionalExtra("deviceName") ?: "pilot-android-emulator"
        val pushToken = optionalExtra("pushToken") ?: "pilot-emulator-push-token"

        val manager = DeviceRuntimeSessionManager(
            sessionStore = AndroidKeystoreSecureDeviceSessionStore.create(this@PilotDeviceActivationActivity),
            transport = HttpDeviceRuntimeTransport(baseUrl),
            runtimeBaseUrl = baseUrl
        )

        manager.activate(
            tenantId = tenantId,
            externalUserId = externalUserId,
            activationCode = activationCode,
            integrationAccessToken = integrationAccessToken,
            deviceName = deviceName,
            pushToken = pushToken
        )
    }

    private suspend fun listPendingFromIntent(): String = withContext(Dispatchers.IO) {
        val baseUrl = requiredExtra("baseUrl")
        val manager = DeviceRuntimeSessionManager(
            sessionStore = AndroidKeystoreSecureDeviceSessionStore.create(this@PilotDeviceActivationActivity),
            transport = HttpDeviceRuntimeTransport(baseUrl),
            runtimeBaseUrl = baseUrl
        )

        val pending = manager.listPendingPushApprovals()
        "Pending push approvals: ${pending.size}" +
            pending.joinToString(separator = "") { challenge ->
                "\n${challenge.id} ${challenge.operationType} ${challenge.operationDisplayName.orEmpty()}"
            }
    }

    private fun requiredExtra(name: String): String {
        return optionalExtra(name)
            ?: throw IllegalArgumentException("Missing intent extra '$name'.")
    }

    private fun optionalExtra(name: String): String? {
        return intent.getStringExtra(name)
            ?.trim()
            ?.takeIf(String::isNotBlank)
    }
}
