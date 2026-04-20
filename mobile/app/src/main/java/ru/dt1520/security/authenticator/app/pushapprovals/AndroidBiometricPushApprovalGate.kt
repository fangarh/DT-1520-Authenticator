package ru.dt1520.security.authenticator.app.pushapprovals

import androidx.biometric.BiometricManager
import androidx.biometric.BiometricManager.Authenticators.BIOMETRIC_STRONG
import androidx.biometric.BiometricManager.Authenticators.DEVICE_CREDENTIAL
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import java.util.concurrent.Executor
import kotlin.coroutines.resume
import kotlin.coroutines.suspendCoroutine
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval

internal class AndroidBiometricPushApprovalGate(
    private val activity: FragmentActivity,
    private val biometricManager: BiometricManager = BiometricManager.from(activity),
    private val executor: Executor = ContextCompat.getMainExecutor(activity)
) : PushApprovalBiometricGate {
    override suspend fun verifyApproval(
        challenge: PendingPushApproval
    ): PushApprovalBiometricGateResult {
        if (activity.isFinishing || activity.isDestroyed) {
            return PushApprovalBiometricGateResult.Rejected(
                "Локальная защита устройства сейчас недоступна. Повторите попытку на активном экране."
            )
        }

        val allowedAuthenticators = resolveAllowedAuthenticators()
            ?: return PushApprovalBiometricGateResult.Rejected(
                unavailableMessageFor(resolvePrimaryAvailabilityCode())
            )

        return suspendCoroutine { continuation ->
            val prompt = BiometricPrompt(
                activity,
                executor,
                object : BiometricPrompt.AuthenticationCallback() {
                    override fun onAuthenticationSucceeded(
                        result: BiometricPrompt.AuthenticationResult
                    ) {
                        continuation.resume(PushApprovalBiometricGateResult.Verified)
                    }

                    override fun onAuthenticationError(
                        errorCode: Int,
                        errString: CharSequence
                    ) {
                        continuation.resume(
                            PushApprovalBiometricGateResult.Rejected(
                                authenticationErrorMessageFor(errorCode)
                            )
                        )
                    }
                }
            )

            prompt.authenticate(
                BiometricPrompt.PromptInfo.Builder()
                    .setTitle("Подтвердите запрос")
                    .setSubtitle(
                        challenge.operationDisplayName?.takeIf { it.isNotBlank() }
                            ?: "Для подтверждения операции нужна локальная аутентификация."
                    )
                    .setDescription(
                        "Используйте биометрию или device credential этого устройства."
                    )
                    .setAllowedAuthenticators(allowedAuthenticators)
                    .build()
            )
        }
    }

    private fun resolveAllowedAuthenticators(): Int? {
        val preferred = BIOMETRIC_STRONG or DEVICE_CREDENTIAL
        if (canAuthenticate(preferred)) {
            return preferred
        }
        if (canAuthenticate(BIOMETRIC_STRONG)) {
            return BIOMETRIC_STRONG
        }
        if (canAuthenticate(DEVICE_CREDENTIAL)) {
            return DEVICE_CREDENTIAL
        }

        return null
    }

    private fun canAuthenticate(authenticators: Int): Boolean {
        return when (biometricManager.canAuthenticate(authenticators)) {
            BiometricManager.BIOMETRIC_SUCCESS,
            BiometricManager.BIOMETRIC_STATUS_UNKNOWN -> true
            else -> false
        }
    }

    private fun resolvePrimaryAvailabilityCode(): Int {
        return biometricManager.canAuthenticate(BIOMETRIC_STRONG or DEVICE_CREDENTIAL)
    }

    private fun unavailableMessageFor(errorCode: Int): String {
        return when (errorCode) {
            BiometricManager.BIOMETRIC_ERROR_NONE_ENROLLED ->
                "На устройстве не настроена биометрия или device credential, поэтому запрос нельзя подтвердить."

            BiometricManager.BIOMETRIC_ERROR_NO_HARDWARE,
            BiometricManager.BIOMETRIC_ERROR_HW_UNAVAILABLE ->
                "Локальная защита устройства недоступна. Повторите попытку позже."

            else ->
                "Локальная защита устройства недоступна для подтверждения этого запроса."
        }
    }

    private fun authenticationErrorMessageFor(errorCode: Int): String {
        return when (errorCode) {
            BiometricPrompt.ERROR_USER_CANCELED,
            BiometricPrompt.ERROR_CANCELED,
            BiometricPrompt.ERROR_NEGATIVE_BUTTON ->
                "Локальное подтверждение было отменено."

            BiometricPrompt.ERROR_LOCKOUT,
            BiometricPrompt.ERROR_LOCKOUT_PERMANENT ->
                "Локальная защита временно заблокирована. Повторите попытку позже."

            BiometricPrompt.ERROR_NO_BIOMETRICS,
            BiometricPrompt.ERROR_NO_DEVICE_CREDENTIAL ->
                "На устройстве не настроена биометрия или device credential, поэтому запрос нельзя подтвердить."

            else ->
                "Локальная аутентификация не завершилась. Повторите попытку еще раз."
        }
    }
}
