package ru.dt1520.security.authenticator.feature.totpcodes

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpCredential
import ru.dt1520.security.authenticator.totp.domain.TotpSecret

class TotpCodesPresenterTest {
    @Test
    fun sortsSummariesByDisplayNameAndBuildsOfflineCodes() {
        val laterAccount = TotpCredential(
            account = TotpAccountDescriptor(
                issuer = "Zeta",
                accountName = "operator@example.local"
            ),
            secret = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP")
        )
        val earlierAccount = TotpCredential(
            account = TotpAccountDescriptor(
                issuer = "Alpha",
                accountName = "operator@example.local"
            ),
            secret = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP")
        )

        val state = TotpCodesPresenter.present(
            credentials = listOf(laterAccount, earlierAccount),
            epochSeconds = 59L
        )

        assertEquals(
            listOf(
                "Alpha (operator@example.local)",
                "Zeta (operator@example.local)"
            ),
            state.summaries.map { it.account.displayName }
        )
        assertEquals(2, state.summaries.size)
        assertTrue(state.summaries.all { it.code.length == 6 })
    }

    @Test
    fun returnsEmptyStateWhenNoCredentialsAreStored() {
        val state = TotpCodesPresenter.present(
            credentials = emptyList(),
            epochSeconds = 59L
        )

        assertTrue(state.isEmpty)
        assertEquals(
            TotpCodesUiState.DEFAULT_EMPTY_MESSAGE,
            state.emptyMessage
        )
    }
}
