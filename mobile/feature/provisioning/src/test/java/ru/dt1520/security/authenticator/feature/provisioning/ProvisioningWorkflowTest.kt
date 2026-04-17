package ru.dt1520.security.authenticator.feature.provisioning

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class ProvisioningWorkflowTest {
    @Test
    fun previewOtpAuthImportBuildsPreviewAndClearsFeedback() {
        val initialState = ProvisioningWorkflowState(
            draft = ProvisioningDraft(
                otpAuthUri = "otpauth://totp/DT1520:operator?secret=JBSWY3DPEHPK3PXP&issuer=DT1520"
            ),
            errorMessage = "old-error",
            successMessage = "old-success"
        )

        val nextState = ProvisioningWorkflow.previewOtpAuthImport(initialState)

        assertEquals("DT1520 (operator)", nextState.preview?.summary)
        assertNull(nextState.errorMessage)
        assertNull(nextState.successMessage)
    }

    @Test
    fun previewManualImportReturnsValidationErrorForBadSecret() {
        val state = ProvisioningWorkflowState(
            draft = ProvisioningDraft(
                manualIssuer = "DT 1520",
                manualAccountName = "operator@example.local",
                manualSecret = "not-base32!"
            )
        )

        val nextState = ProvisioningWorkflow.previewManualImport(state)

        assertNull(nextState.preview)
        assertNotNull(nextState.errorMessage)
    }

    @Test
    fun markSaveSucceededClearsPreviewAndDraft() {
        val state = ProvisioningWorkflowState(
            draft = ProvisioningDraft(
                manualIssuer = "DT 1520",
                manualAccountName = "operator@example.local",
                manualSecret = "JBSWY3DPEHPK3PXP"
            ),
            preview = ProvisioningDraft(
                manualIssuer = "DT 1520",
                manualAccountName = "operator@example.local",
                manualSecret = "JBSWY3DPEHPK3PXP"
            ).buildManualPreview(),
            isSaving = true
        )

        val nextState = ProvisioningWorkflow.markSaveSucceeded(state)

        assertEquals(ProvisioningDraft(), nextState.draft)
        assertNull(nextState.preview)
        assertEquals("Secret сохранен в защищенное хранилище устройства.", nextState.successMessage)
    }

    @Test
    fun updateDraftDropsStalePreview() {
        val preview = ProvisioningDraft(
            manualIssuer = "DT 1520",
            manualAccountName = "operator@example.local",
            manualSecret = "JBSWY3DPEHPK3PXP"
        ).buildManualPreview()
        val state = ProvisioningWorkflowState(
            draft = ProvisioningDraft(manualIssuer = "DT 1520"),
            preview = preview,
            errorMessage = "error",
            successMessage = "success"
        )

        val nextState = ProvisioningWorkflow.updateDraft(
            state = state,
            draft = state.draft.copy(manualAccountName = "new@example.local")
        )

        assertNull(nextState.preview)
        assertNull(nextState.errorMessage)
        assertNull(nextState.successMessage)
    }
}
