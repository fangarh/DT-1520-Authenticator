package ru.dt1520.security.authenticator.feature.provisioning

import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import kotlinx.coroutines.launch
import ru.dt1520.security.authenticator.core.ui.AppSection

@Composable
fun ProvisioningRoute(
    onSaveImport: suspend (ProvisioningImportPreview) -> Unit,
    modifier: Modifier = Modifier
) {
    var workflowState by remember {
        mutableStateOf(ProvisioningWorkflowState())
    }
    val coroutineScope = rememberCoroutineScope()

    AppSection(
        title = "Provisioning",
        description = "Импортируйте `otpauth://` URI или используйте manual fallback. Секреты не раскрываются повторно после preview/save.",
        modifier = modifier
    ) {
        ProvisioningDraftForm(
            draft = workflowState.draft,
            errorMessage = workflowState.errorMessage,
            successMessage = workflowState.successMessage,
            onDraftChange = { draft ->
                workflowState = ProvisioningWorkflow.updateDraft(workflowState, draft)
            },
            onPreviewOtpAuthImport = {
                workflowState = ProvisioningWorkflow.previewOtpAuthImport(workflowState)
            },
            onPreviewManualImport = {
                workflowState = ProvisioningWorkflow.previewManualImport(workflowState)
            }
        )

        val preview = workflowState.preview
        if (preview != null) {
            ProvisioningPreviewCard(
                preview = preview,
                isSaving = workflowState.isSaving,
                onSave = {
                    coroutineScope.launch {
                        workflowState = ProvisioningWorkflow.markSaveStarted(workflowState)

                        workflowState = runCatching {
                            onSaveImport(preview)
                        }.fold(
                            onSuccess = {
                                ProvisioningWorkflow.markSaveSucceeded(workflowState)
                            },
                            onFailure = {
                                ProvisioningWorkflow.markSaveFailed(workflowState)
                            }
                        )
                    }
                }
            )
        }
    }
}
