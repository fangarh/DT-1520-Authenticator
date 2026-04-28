package ru.dt1520.security.authenticator.feature.deviceonboarding

import org.junit.Assert.assertEquals
import org.junit.Test

class DeviceOnboardingPayloadTest {
    @Test
    fun parseAcceptsCanonicalDeviceActivationPayload() {
        val payload = DeviceOnboardingPayload.parse(
            "  dac_0123456789abcdef0123456789abcdef.secret_PART-123  "
        )

        assertEquals(
            "dac_0123456789abcdef0123456789abcdef.secret_PART-123",
            payload.value
        )
    }

    @Test
    fun parseAcceptsBackendGeneratedBase64SecretCharacters() {
        val payload = DeviceOnboardingPayload.parse(
            "dac_0123456789abcdef0123456789abcdef.AbC+/012=="
        )

        assertEquals(
            "dac_0123456789abcdef0123456789abcdef.AbC+/012==",
            payload.value
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun parseRejectsNonDeviceActivationPayload() {
        DeviceOnboardingPayload.parse("otpauth://totp/example")
    }

    @Test(expected = IllegalArgumentException::class)
    fun parseRejectsPayloadWithUnexpectedCharacters() {
        DeviceOnboardingPayload.parse("dac_0123456789abcdef0123456789abcdef.secret with spaces")
    }
}
