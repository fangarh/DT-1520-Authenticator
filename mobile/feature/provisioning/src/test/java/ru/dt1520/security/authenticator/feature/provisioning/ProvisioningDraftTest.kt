package ru.dt1520.security.authenticator.feature.provisioning

import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm

class ProvisioningDraftTest {
    @Test
    fun previewFlagsAreFalseWhenDraftIsEmpty() {
        val draft = ProvisioningDraft()

        assertFalse(draft.canPreviewOtpAuthImport)
        assertFalse(draft.canPreviewManualImport)
    }

    @Test
    fun canPreviewOtpAuthImportWhenUriIsPresent() {
        assertTrue(
            ProvisioningDraft(
                otpAuthUri = "otpauth://totp/DT1520:user?secret=ABCDEF"
            ).canPreviewOtpAuthImport
        )
    }

    @Test
    fun buildsManualPreviewWithDefaultTotpParameters() {
        val preview = ProvisioningDraft(
            manualIssuer = "DT 1520",
            manualAccountName = "operator@example.local",
            manualSecret = "jbsw-y3dp ehpk-3pxp"
        ).buildManualPreview()

        assertEquals("DT 1520", preview.credential.account.issuer)
        assertEquals("operator@example.local", preview.credential.account.accountName)
        assertEquals(TotpAlgorithm.Sha1, preview.credential.algorithm)
        assertEquals(30, preview.credential.account.periodSeconds)
        assertEquals("DT 1520 (operator@example.local)", preview.summary)
    }

    @Test
    fun buildsOtpAuthPreviewFromUri() {
        val preview = ProvisioningDraft(
            otpAuthUri = "otpauth://totp/DT1520:operator?secret=JBSWY3DPEHPK3PXP&issuer=DT1520&digits=8&algorithm=SHA256"
        ).buildOtpAuthPreview()

        assertEquals("DT1520", preview.credential.account.issuer)
        assertEquals(8, preview.credential.digits)
        assertEquals(TotpAlgorithm.Sha256, preview.credential.algorithm)
    }

    @Test(expected = IllegalArgumentException::class)
    fun rejectsIncompleteManualImport() {
        ProvisioningDraft(
            manualIssuer = "DT 1520",
            manualAccountName = "",
            manualSecret = "JBSWY3DPEHPK3PXP"
        ).buildManualPreview()
    }
}
