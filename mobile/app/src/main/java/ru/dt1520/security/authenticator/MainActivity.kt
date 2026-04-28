package ru.dt1520.security.authenticator

import android.os.Bundle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.fragment.app.FragmentActivity
import ru.dt1520.security.authenticator.app.AuthenticatorApp

class MainActivity : FragmentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            AuthenticatorApp(
                deviceRuntimeBaseUrl = BuildConfig.DEVICE_RUNTIME_BASE_URL.takeIf { it.isNotBlank() }
            )
        }
    }
}
