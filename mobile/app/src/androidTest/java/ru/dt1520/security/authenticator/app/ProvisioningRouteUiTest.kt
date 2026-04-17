package ru.dt1520.security.authenticator.app

import androidx.activity.ComponentActivity
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.dt1520.security.authenticator.core.ui.theme.DT1520AuthenticatorTheme
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningImportPreview
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningRoute
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningTestTags

@RunWith(AndroidJUnit4::class)
class ProvisioningRouteUiTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun invalidOtpAuthUriShowsErrorAndDoesNotRenderPreview() {
        setProvisioningContent()

        composeRule.onNodeWithTag(ProvisioningTestTags.OtpAuthUriField)
            .performTextInput("otpauth://totp/OTPAuth:alice?issuer=OTPAuth")
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewUriButton)
            .performClick()

        composeRule.onNodeWithTag(ProvisioningTestTags.ErrorMessage)
            .assertExists()
            .assertTextContains("TOTP secret is required.")
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard)
            .assertDoesNotExist()
    }

    @Test
    fun validOtpAuthUriPreviewCanBeSavedWithSuccessFeedback() {
        var savedPreview: ProvisioningImportPreview? = null

        setProvisioningContent(
            onSaveImport = { preview ->
                savedPreview = preview
            }
        )

        composeRule.onNodeWithTag(ProvisioningTestTags.OtpAuthUriField)
            .performTextInput(
                "otpauth://totp/OTPAuth:alice?secret=JBSWY3DPEHPK3PXP&issuer=OTPAuth&digits=6&period=30&algorithm=SHA1"
            )
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewUriButton)
            .performClick()

        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard).assertExists()
        composeRule.onNodeWithText("Preview: OTPAuth (alice)").assertExists()
        composeRule.onNodeWithText("Source: Provisioning URI").assertExists()
        composeRule.onNodeWithText("Digits: 6").assertExists()
        composeRule.onNodeWithText("Period: 30s").assertExists()
        composeRule.onNodeWithText("Algorithm: SHA1").assertExists()

        composeRule.onNodeWithTag(ProvisioningTestTags.SaveButton)
            .performClick()
        composeRule.waitForIdle()

        assertNotNull(savedPreview)
        assertEquals("OTPAuth (alice)", savedPreview?.summary)
        composeRule.onNodeWithTag(ProvisioningTestTags.SuccessMessage)
            .assertExists()
            .assertTextContains("Secret сохранен в защищенное хранилище устройства.")
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard)
            .assertDoesNotExist()
    }

    @Test
    fun manualFallbackPreviewUsesManualSourceLabel() {
        setProvisioningContent()

        composeRule.onNodeWithTag(ProvisioningTestTags.ManualIssuerField)
            .performTextInput("Contoso")
        composeRule.onNodeWithTag(ProvisioningTestTags.ManualAccountField)
            .performTextInput("operator@example.local")
        composeRule.onNodeWithTag(ProvisioningTestTags.ManualSecretField)
            .performTextInput("JBSWY3DPEHPK3PXP")
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewManualButton)
            .performClick()

        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard).assertExists()
        composeRule.onNodeWithText("Preview: Contoso (operator@example.local)").assertExists()
        composeRule.onNodeWithText("Source: Manual fallback").assertExists()
    }

    @Test
    fun saveFailureShowsGenericErrorWithoutLeakingThrownMessage() {
        setProvisioningContent(
            onSaveImport = {
                throw IllegalStateException("keystore failure secret=JBSWY3DPEHPK3PXP")
            }
        )

        composeRule.onNodeWithTag(ProvisioningTestTags.ManualIssuerField)
            .performTextInput("Contoso")
        composeRule.onNodeWithTag(ProvisioningTestTags.ManualAccountField)
            .performTextInput("operator@example.local")
        composeRule.onNodeWithTag(ProvisioningTestTags.ManualSecretField)
            .performTextInput("JBSWY3DPEHPK3PXP")
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewManualButton)
            .performClick()
        composeRule.onNodeWithTag(ProvisioningTestTags.SaveButton)
            .performClick()
        composeRule.waitForIdle()

        composeRule.onNodeWithTag(ProvisioningTestTags.ErrorMessage)
            .assertExists()
            .assertTextContains(
                "Не удалось сохранить секрет в защищенное хранилище. Проверьте состояние Android Keystore."
            )
        composeRule.onNodeWithText("keystore failure secret=JBSWY3DPEHPK3PXP")
            .assertDoesNotExist()
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard)
            .assertExists()
    }

    private fun setProvisioningContent(
        onSaveImport: suspend (ProvisioningImportPreview) -> Unit = {}
    ) {
        composeRule.setContent {
            DT1520AuthenticatorTheme {
                ProvisioningRoute(onSaveImport = onSaveImport)
            }
        }
    }
}
