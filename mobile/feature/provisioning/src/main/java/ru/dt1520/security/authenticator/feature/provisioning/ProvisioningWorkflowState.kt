package ru.dt1520.security.authenticator.feature.provisioning

data class ProvisioningWorkflowState(
    val draft: ProvisioningDraft = ProvisioningDraft(),
    val preview: ProvisioningImportPreview? = null,
    val errorMessage: String? = null,
    val successMessage: String? = null,
    val isSaving: Boolean = false
)

object ProvisioningWorkflow {
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
                errorMessage = exception.message?.takeIf(String::isNotBlank)
                    ?: "Проверьте provisioning input и повторите импорт.",
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
}
