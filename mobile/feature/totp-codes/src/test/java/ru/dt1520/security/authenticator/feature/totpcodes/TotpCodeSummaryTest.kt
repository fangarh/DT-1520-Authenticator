package ru.dt1520.security.authenticator.feature.totpcodes

import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor

class TotpCodeSummaryTest {
    @Test
    fun marksCodeAsExpiringSoonNearPeriodBoundary() {
        val summary = TotpCodeSummary(
            account = TotpAccountDescriptor(
                issuer = "DT 1520",
                accountName = "operator@example.local"
            ),
            code = "123456",
            remainingSeconds = 5
        )

        assertTrue(summary.isExpiringSoon)
    }

    @Test
    fun keepsCodeStableWhenTimeBudgetIsLargeEnough() {
        val summary = TotpCodeSummary(
            account = TotpAccountDescriptor(
                issuer = "DT 1520",
                accountName = "operator@example.local"
            ),
            code = "123456",
            remainingSeconds = 19
        )

        assertFalse(summary.isExpiringSoon)
    }

    @Test
    fun formatsSixDigitCodeIntoReadableGroups() {
        val summary = TotpCodeSummary(
            account = TotpAccountDescriptor(
                issuer = "DT 1520",
                accountName = "operator@example.local"
            ),
            code = "123456",
            remainingSeconds = 19
        )

        assertEquals("123 456", summary.formattedCode)
    }

    @Test
    fun formatsEightDigitCodeIntoReadableGroups() {
        val summary = TotpCodeSummary(
            account = TotpAccountDescriptor(
                issuer = "DT 1520",
                accountName = "operator@example.local"
            ),
            code = "12345678",
            remainingSeconds = 19
        )

        assertEquals("1234 5678", summary.formattedCode)
    }
}
