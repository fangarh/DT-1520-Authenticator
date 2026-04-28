package ru.dt1520.security.authenticator.feature.deviceonboarding

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import ru.dt1520.security.authenticator.core.ui.AppSection

@Composable
fun DeviceOnboardingRoute(
    onScanQrPayload: suspend () -> String?,
    onActivatePayload: suspend (DeviceOnboardingPayload) -> DeviceOnboardingActivationResult,
    modifier: Modifier = Modifier
) {
    var state by remember {
        mutableStateOf(DeviceOnboardingWorkflowState())
    }
    val coroutineScope = rememberCoroutineScope()

    AppSection(
        title = "Device onboarding",
        description = "Сканируйте QR от оператора, чтобы подключить это устройство к push approval runtime.",
        modifier = modifier.testTag(DeviceOnboardingTestTags.Section)
    ) {
        Column(
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Button(
                onClick = {
                    coroutineScope.launch {
                        val scannedPayload = onScanQrPayload()
                        state = if (scannedPayload == null) {
                            state.copy(errorMessage = null, successMessage = null)
                        } else {
                            DeviceOnboardingWorkflow.acceptScannedPayload(state, scannedPayload)
                        }
                    }
                },
                enabled = !state.isActivating,
                modifier = Modifier.testTag(DeviceOnboardingTestTags.ScanButton)
            ) {
                Text("Scan QR")
            }

            OutlinedTextField(
                value = state.draftPayload,
                onValueChange = { value ->
                    state = DeviceOnboardingWorkflow.updateDraft(state, value)
                },
                label = { Text("Activation payload") },
                minLines = 2,
                maxLines = 4,
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 96.dp)
                    .testTag(DeviceOnboardingTestTags.PayloadField)
            )

            Button(
                onClick = {
                    state = DeviceOnboardingWorkflow.acceptDraftPayload(state)
                },
                enabled = !state.isActivating,
                modifier = Modifier.testTag(DeviceOnboardingTestTags.AcceptPayloadButton)
            ) {
                Text("Use payload")
            }

            state.acceptedPayload?.let { payload ->
                Button(
                    onClick = {
                        coroutineScope.launch {
                            state = DeviceOnboardingWorkflow.markActivationStarted(state)
                            val result = runCatching {
                                onActivatePayload(payload)
                            }.getOrElse {
                                DeviceOnboardingActivationResult.Failure(
                                    userMessage = "Не удалось подключить устройство. Проверьте QR и сеть."
                                )
                            }
                            state = DeviceOnboardingWorkflow.completeActivation(state, result)
                        }
                    },
                    enabled = !state.isActivating,
                    modifier = Modifier.testTag(DeviceOnboardingTestTags.ActivateButton)
                ) {
                    Text(if (state.isActivating) "Activating..." else "Activate device")
                }
            }

            state.errorMessage?.let { message ->
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.testTag(DeviceOnboardingTestTags.ErrorMessage)
                )
            }
            state.successMessage?.let { message ->
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.primary,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.testTag(DeviceOnboardingTestTags.SuccessMessage)
                )
            }
        }
    }
}
