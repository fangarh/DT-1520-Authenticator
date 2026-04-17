package ru.dt1520.security.authenticator.totp.domain

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class TotpSecretTest {
    @Test
    fun acceptsLowercaseAndGroupedBase32Secret() {
        val first = TotpSecret.fromBase32("jbsw-y3dp ehpk-3pxp")
        val second = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP")

        assertEquals(first, second)
        assertEquals("TotpSecret(**redacted**)", first.toString())
        assertEquals("JBSWY3DPEHPK3PXP", first.toBase32())
    }

    @Test
    fun keepsSecretMaterialOutOfStringRepresentation() {
        val secret = TotpSecret.fromBase32("JBSWY3DPEHPK3PXP")

        assertFalse(secret.toString().contains("JBSWY3DPEHPK3PXP"))
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsInvalidBase32Character() {
        TotpSecret.fromBase32("JBSWY3DPEHPK3PX!")
    }
}
