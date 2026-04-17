package ru.dt1520.security.authenticator.feature.provisioning

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp

@Composable
internal fun ProvisioningDraftForm(
    draft: ProvisioningDraft,
    errorMessage: String?,
    successMessage: String?,
    onDraftChange: (ProvisioningDraft) -> Unit,
    onPreviewOtpAuthImport: () -> Unit,
    onPreviewManualImport: () -> Unit
) {
    Column(
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        OutlinedTextField(
            modifier = Modifier.testTag(ProvisioningTestTags.OtpAuthUriField),
            value = draft.otpAuthUri,
            onValueChange = { value ->
                onDraftChange(draft.copy(otpAuthUri = value))
            },
            label = { Text("Provisioning URI") },
            placeholder = { Text("otpauth://totp/...") },
            visualTransformation = PasswordVisualTransformation(),
            singleLine = true
        )
        Button(
            modifier = Modifier.testTag(ProvisioningTestTags.PreviewUriButton),
            onClick = onPreviewOtpAuthImport,
            enabled = draft.canPreviewOtpAuthImport
        ) {
            Text("Preview URI import")
        }

        OutlinedTextField(
            modifier = Modifier.testTag(ProvisioningTestTags.ManualIssuerField),
            value = draft.manualIssuer,
            onValueChange = { value ->
                onDraftChange(draft.copy(manualIssuer = value))
            },
            label = { Text("Manual issuer") },
            singleLine = true
        )
        OutlinedTextField(
            modifier = Modifier.testTag(ProvisioningTestTags.ManualAccountField),
            value = draft.manualAccountName,
            onValueChange = { value ->
                onDraftChange(draft.copy(manualAccountName = value))
            },
            label = { Text("Manual account") },
            singleLine = true
        )
        OutlinedTextField(
            modifier = Modifier.testTag(ProvisioningTestTags.ManualSecretField),
            value = draft.manualSecret,
            onValueChange = { value ->
                onDraftChange(draft.copy(manualSecret = value))
            },
            label = { Text("Manual Base32 secret") },
            visualTransformation = PasswordVisualTransformation(),
            singleLine = true
        )
        Button(
            modifier = Modifier.testTag(ProvisioningTestTags.PreviewManualButton),
            onClick = onPreviewManualImport,
            enabled = draft.canPreviewManualImport
        ) {
            Text("Preview manual import")
        }

        if (errorMessage != null) {
            Text(
                text = errorMessage,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.testTag(ProvisioningTestTags.ErrorMessage)
            )
        }

        if (successMessage != null) {
            Text(
                text = successMessage,
                color = MaterialTheme.colorScheme.primary,
                modifier = Modifier.testTag(ProvisioningTestTags.SuccessMessage)
            )
        }
    }
}
