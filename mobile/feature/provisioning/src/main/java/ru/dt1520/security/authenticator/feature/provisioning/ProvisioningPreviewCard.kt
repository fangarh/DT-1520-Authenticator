package ru.dt1520.security.authenticator.feature.provisioning

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp

@Composable
internal fun ProvisioningPreviewCard(
    preview: ProvisioningImportPreview,
    isSaving: Boolean,
    onSave: () -> Unit
) {
    Column(
        modifier = Modifier.testTag(ProvisioningTestTags.PreviewCard),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Text("Preview: ${preview.summary}")
        Text("Source: ${preview.source.label}")
        Text("Digits: ${preview.credential.digits}")
        Text("Period: ${preview.credential.account.periodSeconds}s")
        Text("Algorithm: ${preview.credential.algorithm.parameterValue}")
        Button(
            modifier = Modifier.testTag(ProvisioningTestTags.SaveButton),
            onClick = onSave,
            enabled = !isSaving
        ) {
            Text(if (isSaving) "Saving..." else "Save secure secret")
        }
    }
}
