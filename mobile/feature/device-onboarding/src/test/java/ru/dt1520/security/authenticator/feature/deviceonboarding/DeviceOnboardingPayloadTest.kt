package ru.dt1520.security.authenticator.feature.deviceonboarding

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Test

class DeviceOnboardingPayloadTest {
    @Test
    fun parseAcceptsVersionOneQrEnvelope() {
        val payload = DeviceOnboardingPayload.parse(
            """
            {
              "v": 1,
              "runtimeBaseUrl": "https://admin.ghostring.ru:18443",
              "activationPayload": "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
            }
            """.trimIndent()
        )

        assertEquals(
            "dac_0123456789abcdef0123456789abcdef.secret_PART-123",
            payload.activationPayload
        )
        assertEquals(payload.activationPayload, payload.value)
        assertEquals("https://admin.ghostring.ru:18443", payload.runtimeBaseUrl)
    }

    @Test
    fun parseAcceptsCanonicalDeviceActivationPayload() {
        val payload = DeviceOnboardingPayload.parse(
            "  dac_0123456789abcdef0123456789abcdef.secret_PART-123  "
        )

        assertEquals(
            "dac_0123456789abcdef0123456789abcdef.secret_PART-123",
            payload.activationPayload
        )
        assertEquals(payload.activationPayload, payload.value)
        assertNull(payload.runtimeBaseUrl)
    }

    @Test
    fun parseAcceptsBackendGeneratedBase64SecretCharacters() {
        val payload = DeviceOnboardingPayload.parse(
            "dac_0123456789abcdef0123456789abcdef.AbC+/012=="
        )

        assertEquals(
            "dac_0123456789abcdef0123456789abcdef.AbC+/012==",
            payload.activationPayload
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

    @Test
    fun parseRejectsEnvelopeWithInvalidRuntimeUrlWithoutEchoingUrl() {
        val exception = runCatching {
            DeviceOnboardingPayload.parse(
                """
                {
                  "v": 1,
                  "runtimeBaseUrl": "https://user:secret@example.test",
                  "activationPayload": "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
                }
                """.trimIndent()
            )
        }.exceptionOrNull()

        assertEquals("Runtime URL has unsupported format.", exception?.message)
        assertFalse(exception?.message.orEmpty().contains("user:secret"))
        assertFalse(exception?.message.orEmpty().contains("example.test"))
    }

    @Test(expected = IllegalArgumentException::class)
    fun parseRejectsEnvelopeWithHttpRuntimeUrl() {
        DeviceOnboardingPayload.parse(
            """
            {
              "v": 1,
              "runtimeBaseUrl": "http://admin.ghostring.ru:18443",
              "activationPayload": "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
            }
            """.trimIndent()
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun parseRejectsEnvelopeWithMissingHostRuntimeUrl() {
        DeviceOnboardingPayload.parse(
            """
            {
              "v": 1,
              "runtimeBaseUrl": "https:///runtime",
              "activationPayload": "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
            }
            """.trimIndent()
        )
    }

    @Test(expected = IllegalArgumentException::class)
    fun parseRejectsMalformedEnvelope() {
        DeviceOnboardingPayload.parse(
            """
            {
              "v": 1,
              "runtimeBaseUrl": "https://admin.ghostring.ru:18443"
            }
            """.trimIndent()
        )
    }
}
