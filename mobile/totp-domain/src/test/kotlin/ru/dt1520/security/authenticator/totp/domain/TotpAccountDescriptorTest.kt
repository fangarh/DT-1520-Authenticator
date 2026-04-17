package ru.dt1520.security.authenticator.totp.domain

import org.junit.Assert.assertEquals
import org.junit.Test

class TotpAccountDescriptorTest {
    @Test
    fun usesIssuerAndAccountNameForDisplayName() {
        val descriptor = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local"
        )

        assertEquals("DT 1520 (operator@example.local)", descriptor.displayName)
    }

    @Test
    fun buildsCanonicalLabelFromIssuerAndAccountName() {
        val descriptor = TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local"
        )

        assertEquals("DT 1520:operator@example.local", descriptor.canonicalLabel)
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsBlankIssuer() {
        TotpAccountDescriptor(
            issuer = "",
            accountName = "operator@example.local"
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsNonPositivePeriod() {
        TotpAccountDescriptor(
            issuer = "DT 1520",
            accountName = "operator@example.local",
            periodSeconds = 0
        )
    }
}
