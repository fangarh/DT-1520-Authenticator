package ru.dt1520.security.authenticator.app

import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onFirst
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.util.LinkedHashMap
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningTestTags
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodeSummary
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodesTestTags
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.StoredTotpSecret
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpCodeGenerator
import ru.dt1520.security.authenticator.totp.domain.TotpSecret

@RunWith(AndroidJUnit4::class)
class AuthenticatorAppUiTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun manualImportSaveRefreshRemoveReturnsToEmptyState() {
        val store = InMemorySecureTotpSecretStore()

        composeRule.setContent {
            AuthenticatorApp(
                secureStoreOverride = store,
                currentEpochSecondsProvider = { 59L },
                clockTickDelayMillis = 60_000L
            )
        }

        composeRule.onNodeWithTag(ProvisioningTestTags.ManualIssuerField)
            .performTextInput("Contoso")
        composeRule.onNodeWithTag(ProvisioningTestTags.ManualAccountField)
            .performTextInput("operator@example.local")
        composeRule.onNodeWithTag(ProvisioningTestTags.ManualSecretField)
            .performTextInput("JBSWY3DPEHPK3PXP")
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewManualButton)
            .performClick()

        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard).assertExists()
        composeRule.onNodeWithTag(ProvisioningTestTags.SaveButton)
            .performScrollTo()
            .performClick()
        composeRule.waitForIdle()

        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithTag(ProvisioningTestTags.PreviewCard)
                .fetchSemanticsNodes().isEmpty()
        }
        composeRule.onNodeWithTag(ProvisioningTestTags.PreviewCard).assertDoesNotExist()
        composeRule.onNodeWithTag(ProvisioningTestTags.SuccessMessage).assertExists()
        composeRule.onNodeWithText("Contoso (operator@example.local)").assertExists()
        composeRule.onNodeWithText(expectedCode()).assertExists()
        composeRule.onNodeWithText("Обновится через 1с").assertExists()

        assertEquals(1, store.snapshot().size)

        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithTag(TotpCodesTestTags.RemoveButton)
                .fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onAllNodesWithTag(TotpCodesTestTags.RemoveButton).onFirst()
            .performScrollTo()
            .performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithTag(TotpCodesTestTags.ConfirmRemoveButton)
                .fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithTag(TotpCodesTestTags.ConfirmRemoveButton)
            .performScrollTo()
            .performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            store.snapshot().isEmpty()
        }
        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithTag(TotpCodesTestTags.EmptyStateMessage)
                .fetchSemanticsNodes().isNotEmpty()
        }

        composeRule.onNodeWithTag(TotpCodesTestTags.EmptyStateMessage).assertExists()
        composeRule.onNodeWithText("Contoso (operator@example.local)").assertDoesNotExist()
        assertEquals(0, store.snapshot().size)
    }

    private fun expectedCode(): String {
        val credential = StoredTotpSecret(
            account = TotpAccountDescriptor(
                issuer = "Contoso",
                accountName = "operator@example.local"
            ),
            secret = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP").toBase32()
        ).toCredential()

        val codeState = TotpCodeGenerator.generate(credential, epochSeconds = 59L)

        return TotpCodeSummary(
            account = credential.account,
            code = codeState.code,
            remainingSeconds = codeState.remainingSeconds
        ).formattedCode
    }

    private class InMemorySecureTotpSecretStore : SecureTotpSecretStore {
        private val secrets = LinkedHashMap<TotpAccountDescriptor, StoredTotpSecret>()

        override suspend fun list(): List<TotpAccountDescriptor> = secrets.keys.toList()

        override suspend fun read(account: TotpAccountDescriptor): StoredTotpSecret? = secrets[account]

        override suspend fun save(secret: StoredTotpSecret) {
            secrets[secret.account] = secret
        }

        override suspend fun delete(account: TotpAccountDescriptor) {
            secrets.remove(account)
        }

        fun snapshot(): List<StoredTotpSecret> = secrets.values.toList()
    }
}
