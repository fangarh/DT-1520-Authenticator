package ru.dt1520.security.authenticator.feature.deviceonboarding

import java.util.UUID
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class DeviceOnboardingWorkflowTest {
    @Test
    fun acceptScannedPayloadStoresValidatedPayloadWithoutRawErrorLeakage() {
        val state = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            VALID_PAYLOAD
        )

        assertEquals(VALID_PAYLOAD, state.acceptedPayload?.value)
        assertNull(state.acceptedPayload?.runtimeBaseUrl)
        assertEquals("QR payload готов к активации.", state.successMessage)
        assertNull(state.errorMessage)
    }

    @Test
    fun acceptScannedEnvelopeStoresRuntimeUrlAndActivationPayload() {
        val state = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            """
            {
              "v": 1,
              "runtimeBaseUrl": "https://admin.ghostring.ru:18443",
              "activationPayload": "$VALID_PAYLOAD"
            }
            """.trimIndent()
        )

        assertEquals(VALID_PAYLOAD, state.acceptedPayload?.activationPayload)
        assertEquals("https://admin.ghostring.ru:18443", state.acceptedPayload?.runtimeBaseUrl)
        assertEquals(VALID_PAYLOAD, state.draftPayload)
        assertEquals("QR payload готов к активации.", state.successMessage)
        assertNull(state.errorMessage)
    }

    @Test
    fun invalidPayloadUsesSanitizedMessage() {
        val state = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            "not-a-qr-payload"
        )

        assertNull(state.acceptedPayload)
        assertEquals("QR payload не подходит для подключения устройства.", state.errorMessage)
    }

    @Test
    fun successfulActivationClearsPayloadFromState() {
        val accepted = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            VALID_PAYLOAD
        )

        val completed = DeviceOnboardingWorkflow.completeActivation(
            accepted,
            DeviceOnboardingActivationResult.Success(UUID.randomUUID())
        )

        assertNull(completed.acceptedPayload)
        assertEquals("", completed.draftPayload)
        assertNotNull(completed.successMessage)
    }

    private companion object {
        const val VALID_PAYLOAD = "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
    }
}
