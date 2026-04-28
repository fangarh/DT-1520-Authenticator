package ru.dt1520.security.authenticator.app

import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.util.UUID
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingActivationResult
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingPayload
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingTestTags

@RunWith(AndroidJUnit4::class)
class DeviceOnboardingUiTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun scannedPayloadCanActivateDeviceAndClearsPayload() {
        var activatedPayload: String? = null

        composeRule.setContent {
            AuthenticatorApp(
                onScanDeviceOnboardingQrPayload = { VALID_PAYLOAD },
                onActivateDeviceOnboardingPayload = { payload ->
                    activatedPayload = payload.value
                    DeviceOnboardingActivationResult.Success(UUID.randomUUID())
                },
                currentEpochSecondsProvider = { 59L },
                clockTickDelayMillis = 60_000L
            )
        }

        composeRule.onNodeWithTag(DeviceOnboardingTestTags.ScanButton)
            .performScrollTo()
            .performClick()
        composeRule.onNodeWithTag(DeviceOnboardingTestTags.ActivateButton)
            .performScrollTo()
            .performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            activatedPayload == VALID_PAYLOAD
        }

        composeRule.onNodeWithText("Устройство подключено.", substring = true).assertExists()
        composeRule.onNodeWithText(VALID_PAYLOAD).assertDoesNotExist()
    }

    @Test
    fun invalidPastedPayloadShowsSanitizedError() {
        composeRule.setContent {
            AuthenticatorApp(
                onScanDeviceOnboardingQrPayload = { null },
                onActivateDeviceOnboardingPayload = {
                    DeviceOnboardingActivationResult.Failure("should not be called")
                },
                currentEpochSecondsProvider = { 59L },
                clockTickDelayMillis = 60_000L
            )
        }

        composeRule.onNodeWithTag(DeviceOnboardingTestTags.PayloadField)
            .performScrollTo()
            .performTextInput("otpauth://totp/not-device")
        composeRule.onNodeWithTag(DeviceOnboardingTestTags.AcceptPayloadButton)
            .performScrollTo()
            .performClick()

        composeRule.onNodeWithTag(DeviceOnboardingTestTags.ErrorMessage).assertExists()
        composeRule.onNodeWithText("QR payload не подходит", substring = true).assertExists()
    }

    private companion object {
        const val VALID_PAYLOAD = "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
    }
}
