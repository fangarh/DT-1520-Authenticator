package ru.dt1520.security.authenticator.app

import androidx.activity.ComponentActivity
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onFirst
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.dt1520.security.authenticator.core.ui.theme.DT1520AuthenticatorTheme
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodesRoute
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodeSummary
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodesTestTags
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpCodeGenerator
import ru.dt1520.security.authenticator.totp.domain.TotpCredential
import ru.dt1520.security.authenticator.totp.domain.TotpSecret

@RunWith(AndroidJUnit4::class)
class TotpCodesRouteUiTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun emptyStateIsRenderedWhenNoCredentialsExist() {
        setTotpCodesContent(credentials = emptyList())

        composeRule.onNodeWithTag(TotpCodesTestTags.EmptyStateMessage)
            .assertExists()
        composeRule.onNodeWithText(
            "Сохраните первый TOTP secret через Provisioning, чтобы увидеть офлайн-коды."
        ).assertExists()
    }

    @Test
    fun removeFlowShowsConfirmationAndCanBeCancelledSafely() {
        val credential = sampleCredential()

        setTotpCodesContent(credentials = listOf(credential))

        composeRule.onNodeWithText("Contoso (operator@example.local)").assertExists()
        composeRule.onAllNodesWithTag(TotpCodesTestTags.RemoveButton).onFirst()
            .performClick()

        composeRule.onNodeWithText(
            "Удаление с устройства необратимо. Секрет останется только в защищенном хранилище до завершения операции."
        ).assertExists()
        composeRule.onNodeWithTag(TotpCodesTestTags.ConfirmRemoveButton).assertExists()
        composeRule.onNodeWithTag(TotpCodesTestTags.CancelRemoveButton)
            .performClick()

        composeRule.onNodeWithTag(TotpCodesTestTags.ConfirmRemoveButton)
            .assertDoesNotExist()
    }

    @Test
    fun confirmedRemovalInvokesCallbackForSelectedAccount() {
        val credential = sampleCredential()
        var removedAccount: TotpAccountDescriptor? = null

        setTotpCodesContent(
            credentials = listOf(credential),
            onRemoveAccount = { account ->
                removedAccount = account
            }
        )

        composeRule.onAllNodesWithTag(TotpCodesTestTags.RemoveButton).onFirst()
            .performClick()
        composeRule.onNodeWithTag(TotpCodesTestTags.ConfirmRemoveButton)
            .performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            removedAccount != null
        }

        assertEquals(credential.account, removedAccount)
        composeRule.onNodeWithTag(TotpCodesTestTags.ConfirmRemoveButton)
            .assertDoesNotExist()
    }

    @Test
    fun runtimeSummariesRenderSortedAccountsCodesAndCountdown() {
        val alpha = TotpCredential(
            account = TotpAccountDescriptor(
                issuer = "Alpha",
                accountName = "operator@example.local"
            ),
            secret = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP")
        )
        val zeta = TotpCredential(
            account = TotpAccountDescriptor(
                issuer = "Zeta",
                accountName = "operator@example.local"
            ),
            secret = TotpSecret.fromBase32("KRUGS4ZANFZSAYJA")
        )
        val expectedAlphaCode = formatCode(alpha, epochSeconds = 59L)
        val expectedZetaCode = formatCode(zeta, epochSeconds = 59L)

        setTotpCodesContent(credentials = listOf(zeta, alpha))

        composeRule.onNodeWithText("Alpha (operator@example.local)").assertExists()
        composeRule.onNodeWithText("Zeta (operator@example.local)").assertExists()
        composeRule.onNodeWithText(expectedAlphaCode).assertExists()
        composeRule.onNodeWithText(expectedZetaCode).assertExists()
        composeRule.onAllNodesWithText("Обновится через 1с")
            .assertCountEquals(2)
    }

    private fun setTotpCodesContent(
        credentials: List<TotpCredential>,
        onRemoveAccount: suspend (TotpAccountDescriptor) -> Unit = {}
    ) {
        composeRule.setContent {
            DT1520AuthenticatorTheme {
                TotpCodesRoute(
                    credentials = credentials,
                    currentEpochSeconds = 59L,
                    onRemoveAccount = onRemoveAccount
                )
            }
        }
    }

    private fun sampleCredential(): TotpCredential = TotpCredential(
        account = TotpAccountDescriptor(
            issuer = "Contoso",
            accountName = "operator@example.local"
        ),
        secret = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP")
    )

    private fun formatCode(
        credential: TotpCredential,
        epochSeconds: Long
    ): String {
        val codeState = TotpCodeGenerator.generate(credential, epochSeconds = epochSeconds)

        return TotpCodeSummary(
            account = credential.account,
            code = codeState.code,
            remainingSeconds = codeState.remainingSeconds
        ).formattedCode
    }
}
