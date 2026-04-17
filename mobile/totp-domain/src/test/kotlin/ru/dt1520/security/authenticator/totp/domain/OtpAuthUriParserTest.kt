package ru.dt1520.security.authenticator.totp.domain

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class OtpAuthUriParserTest {
    @Test
    fun parsesProvisioningUriWithExplicitIssuerAndDefaults() {
        val credential = OtpAuthUriParser.parse(
            "otpauth://totp/DT%201520:operator%40example.local?secret=JBSWY3DPEHPK3PXP&issuer=DT%201520"
        )

        assertEquals("DT 1520", credential.account.issuer)
        assertEquals("operator@example.local", credential.account.accountName)
        assertEquals(30, credential.account.periodSeconds)
        assertEquals(6, credential.digits)
        assertEquals(TotpAlgorithm.Sha1, credential.algorithm)
    }

    @Test
    fun parsesProvisioningUriWithCustomDigitsPeriodAndAlgorithm() {
        val credential = OtpAuthUriParser.parse(
            "otpauth://totp/DT%201520:operator?secret=JBSWY3DPEHPK3PXP&issuer=DT%201520&digits=8&period=45&algorithm=SHA512"
        )

        assertEquals(8, credential.digits)
        assertEquals(45, credential.account.periodSeconds)
        assertEquals(TotpAlgorithm.Sha512, credential.algorithm)
    }

    @Test
    fun decodesUtf8IssuerAndAccountName() {
        val credential = OtpAuthUriParser.parse(
            "otpauth://totp/%D0%94%D0%A2%201520:%D0%BE%D0%BF%D0%B5%D1%80%D0%B0%D1%82%D0%BE%D1%80%40example.local?secret=JBSWY3DPEHPK3PXP&issuer=%D0%94%D0%A2%201520"
        )

        assertEquals("ДТ 1520", credential.account.issuer)
        assertEquals("оператор@example.local", credential.account.accountName)
    }

    @Test
    fun keepsSecretRedactedInStringRepresentation() {
        val credential = OtpAuthUriParser.parse(
            "otpauth://totp/DT1520:operator?secret=JBSWY3DPEHPK3PXP&issuer=DT1520"
        )

        assertTrue(credential.toString().contains("**redacted**"))
        assertTrue(!credential.toString().contains("JBSWY3DPEHPK3PXP"))
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsIssuerMismatchBetweenLabelAndQuery() {
        OtpAuthUriParser.parse(
            "otpauth://totp/DT1520:operator?secret=JBSWY3DPEHPK3PXP&issuer=Other"
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsDuplicateQueryParameters() {
        OtpAuthUriParser.parse(
            "otpauth://totp/DT1520:operator?secret=JBSWY3DPEHPK3PXP&secret=OTHER&issuer=DT1520"
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsUnsupportedScheme() {
        OtpAuthUriParser.parse(
            "https://example.local/totp?secret=JBSWY3DPEHPK3PXP&issuer=DT1520"
        )
    }
}
