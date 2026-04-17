package ru.dt1520.security.authenticator.feature.provisioning

data class ProvisioningWorkflowState(
    val draft: ProvisioningDraft = ProvisioningDraft(),
    val preview: ProvisioningImportPreview? = null,
    val errorMessage: String? = null,
    val successMessage: String? = null,
    val isSaving: Boolean = false
)

object ProvisioningWorkflow {
    private val safePreviewValidationMessages = setOf(
        "Invalid otpauth URI.",
        "Unsupported provisioning URI scheme.",
        "Unsupported provisioning URI type.",
        "TOTP secret is required.",
        "Provisioning label is required.",
        "Provisioning issuer is required.",
        "Provisioning issuer must be consistent.",
        "Duplicate otpauth query parameter is not allowed.",
        "Invalid otpauth query parameter.",
        "Invalid percent-encoded value.",
        "Unsupported TOTP algorithm.",
        "Invalid Base32 secret.",
        "digits must not be blank.",
        "digits must be numeric.",
        "period must not be blank.",
        "period must be numeric.",
        "Manual import requires issuer, account name and secret."
    )

    fun updateDraft(
        state: ProvisioningWorkflowState,
        draft: ProvisioningDraft
    ): ProvisioningWorkflowState = state.copy(
        draft = draft,
        preview = null,
        errorMessage = null,
        successMessage = null,
        isSaving = false
    )

    fun previewOtpAuthImport(state: ProvisioningWorkflowState): ProvisioningWorkflowState =
        previewImport(state, ProvisioningDraft::buildOtpAuthPreview)

    fun previewManualImport(state: ProvisioningWorkflowState): ProvisioningWorkflowState =
        previewImport(state, ProvisioningDraft::buildManualPreview)

    fun markSaveStarted(state: ProvisioningWorkflowState): ProvisioningWorkflowState = state.copy(
        isSaving = true,
        errorMessage = null,
        successMessage = null
    )

    fun markSaveSucceeded(state: ProvisioningWorkflowState): ProvisioningWorkflowState =
        state.copy(
            draft = ProvisioningDraft(),
            preview = null,
            errorMessage = null,
            successMessage = "Secret сохранен в защищенное хранилище устройства.",
            isSaving = false
        )

    fun markSaveFailed(state: ProvisioningWorkflowState): ProvisioningWorkflowState = state.copy(
        isSaving = false,
        errorMessage = "Не удалось сохранить секрет в защищенное хранилище. Проверьте состояние Android Keystore."
    )

    private fun previewImport(
        state: ProvisioningWorkflowState,
        previewFactory: ProvisioningDraft.() -> ProvisioningImportPreview
    ): ProvisioningWorkflowState {
        val preview = try {
            state.draft.previewFactory()
        } catch (exception: IllegalArgumentException) {
            return state.copy(
                preview = null,
                errorMessage = sanitizePreviewValidationMessage(exception.message),
                successMessage = null,
                isSaving = false
            )
        }

        return state.copy(
            preview = preview,
            errorMessage = null,
            successMessage = null,
            isSaving = false
        )
    }

    internal fun sanitizePreviewValidationMessage(rawMessage: String?): String =
        rawMessage?.takeIf { it in safePreviewValidationMessages }
            ?: "Проверьте provisioning input и повторите импорт."
}
