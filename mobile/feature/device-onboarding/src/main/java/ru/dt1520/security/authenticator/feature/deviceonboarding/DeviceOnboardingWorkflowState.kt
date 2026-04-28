package ru.dt1520.security.authenticator.feature.deviceonboarding

import java.util.UUID

data class DeviceOnboardingWorkflowState(
    val draftPayload: String = "",
    val acceptedPayload: DeviceOnboardingPayload? = null,
    val errorMessage: String? = null,
    val successMessage: String? = null,
    val isActivating: Boolean = false
)

sealed interface DeviceOnboardingActivationResult {
    data class Success(val deviceId: UUID) : DeviceOnboardingActivationResult

    data class Failure(val userMessage: String) : DeviceOnboardingActivationResult
}

object DeviceOnboardingWorkflow {
    fun updateDraft(
        state: DeviceOnboardingWorkflowState,
        value: String
    ): DeviceOnboardingWorkflowState = state.copy(
        draftPayload = value,
        acceptedPayload = null,
        errorMessage = null,
        successMessage = null,
        isActivating = false
    )

    fun acceptScannedPayload(
        state: DeviceOnboardingWorkflowState,
        rawPayload: String?
    ): DeviceOnboardingWorkflowState = acceptPayload(state, rawPayload, clearDraft = false)

    fun acceptDraftPayload(state: DeviceOnboardingWorkflowState): DeviceOnboardingWorkflowState =
        acceptPayload(state, state.draftPayload, clearDraft = false)

    fun markActivationStarted(state: DeviceOnboardingWorkflowState): DeviceOnboardingWorkflowState =
        state.copy(
            isActivating = true,
            errorMessage = null,
            successMessage = null
        )

    fun completeActivation(
        state: DeviceOnboardingWorkflowState,
        result: DeviceOnboardingActivationResult
    ): DeviceOnboardingWorkflowState {
        return when (result) {
            is DeviceOnboardingActivationResult.Success -> state.copy(
                draftPayload = "",
                acceptedPayload = null,
                errorMessage = null,
                successMessage = "Устройство подключено. Push-запросы будут появляться в этом приложении.",
                isActivating = false
            )

            is DeviceOnboardingActivationResult.Failure -> state.copy(
                errorMessage = result.userMessage,
                successMessage = null,
                isActivating = false
            )
        }
    }

    private fun acceptPayload(
        state: DeviceOnboardingWorkflowState,
        rawPayload: String?,
        clearDraft: Boolean
    ): DeviceOnboardingWorkflowState {
        val payload = try {
            DeviceOnboardingPayload.parse(rawPayload)
        } catch (_: IllegalArgumentException) {
            return state.copy(
                acceptedPayload = null,
                errorMessage = "QR payload не подходит для подключения устройства.",
                successMessage = null,
                isActivating = false
            )
        }

        return state.copy(
            draftPayload = if (clearDraft) "" else payload.value,
            acceptedPayload = payload,
            errorMessage = null,
            successMessage = "QR payload готов к активации.",
            isActivating = false
        )
    }
}
