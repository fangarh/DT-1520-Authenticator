package ru.dt1520.security.authenticator.app.deviceonboarding

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import kotlinx.coroutines.CompletableDeferred

@Composable
internal fun rememberDeviceOnboardingQrScanner(): suspend () -> String? {
    val context = LocalContext.current
    var pendingScan by remember {
        mutableStateOf<CompletableDeferred<String?>?>(null)
    }
    val launcher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        val payload = result.data?.getStringExtra(
            QrDeviceOnboardingScanActivity.EXTRA_QR_PAYLOAD
        )
        pendingScan?.complete(payload)
        pendingScan = null
    }

    return remember(context, launcher) {
        {
            val deferred = CompletableDeferred<String?>()
            pendingScan = deferred
            launcher.launch(QrDeviceOnboardingScanActivity.createIntent(context))
            deferred.await()
        }
    }
}
