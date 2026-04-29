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
    fun acceptScannedCombinedEnvelopeStoresTotpProvisioningPayloadWithoutShowingRawEnvelope() {
        val state = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            """
            {
              "v": 2,
              "runtimeBaseUrl": "https://admin.ghostring.ru:18443",
              "activationPayload": "$VALID_PAYLOAD",
              "totpProvisioningPayload": "$VALID_TOTP_URI"
            }
            """.trimIndent()
        )

        assertEquals(VALID_PAYLOAD, state.acceptedPayload?.activationPayload)
        assertEquals(VALID_TOTP_URI, state.acceptedPayload?.totpProvisioningPayload)
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

    @Test
    fun combinedActivationSuccessMentionsTotpImportAndClearsPayloadFromState() {
        val accepted = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            VALID_PAYLOAD
        )

        val completed = DeviceOnboardingWorkflow.completeActivation(
            accepted,
            DeviceOnboardingActivationResult.Success(UUID.randomUUID(), totpImported = true)
        )

        assertNull(completed.acceptedPayload)
        assertEquals("", completed.draftPayload)
        assertEquals(
            "Устройство подключено, TOTP-код сохранен в защищенное хранилище.",
            completed.successMessage
        )
        assertNull(completed.errorMessage)
    }

    @Test
    fun partialActivationSuccessClearsPayloadAndShowsTotpFailure() {
        val accepted = DeviceOnboardingWorkflow.acceptScannedPayload(
            DeviceOnboardingWorkflowState(),
            VALID_PAYLOAD
        )

        val completed = DeviceOnboardingWorkflow.completeActivation(
            accepted,
            DeviceOnboardingActivationResult.PartialSuccess(
                deviceId = UUID.randomUUID(),
                userMessage = "TOTP-код не удалось сохранить. Повторите импорт из нового QR."
            )
        )

        assertNull(completed.acceptedPayload)
        assertEquals("", completed.draftPayload)
        assertEquals("Устройство подключено. TOTP-код не был сохранен.", completed.successMessage)
        assertEquals("TOTP-код не удалось сохранить. Повторите импорт из нового QR.", completed.errorMessage)
    }

    private companion object {
        const val VALID_PAYLOAD = "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
        const val VALID_TOTP_URI = "otpauth://totp/OTPAuth:user?secret=JBSWY3DPEHPK3PXP&issuer=OTPAuth"
    }
}
