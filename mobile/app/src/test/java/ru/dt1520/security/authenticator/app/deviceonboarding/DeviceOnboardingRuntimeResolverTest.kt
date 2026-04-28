package ru.dt1520.security.authenticator.app.deviceonboarding

import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Test
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingPayload

class DeviceOnboardingRuntimeResolverTest {
    @Test
    fun qrEnvelopeRuntimeUrlTakesPrecedenceOverConfiguredFallback() {
        val payload = DeviceOnboardingPayload.parse(
            """
            {
              "v": 1,
              "runtimeBaseUrl": "https://admin.ghostring.ru:18443",
              "activationPayload": "$VALID_ACTIVATION_PAYLOAD"
            }
            """.trimIndent()
        )

        val target = resolveDeviceOnboardingRuntimeTarget(
            payload = payload,
            configuredBaseUrl = "https://build-config.example.test"
        )

        assertEquals(
            DeviceOnboardingRuntimeTarget.QrEnvelope("https://admin.ghostring.ru:18443"),
            target
        )
    }

    @Test
    fun legacyPayloadUsesConfiguredFallbackWhenAvailable() {
        val payload = DeviceOnboardingPayload.parse(VALID_ACTIVATION_PAYLOAD)

        val target = resolveDeviceOnboardingRuntimeTarget(
            payload = payload,
            configuredBaseUrl = "https://build-config.example.test"
        )

        assertSame(DeviceOnboardingRuntimeTarget.ConfiguredFallback, target)
    }

    @Test
    fun legacyPayloadWithoutConfiguredFallbackRequiresRuntimeUrlInQr() {
        val payload = DeviceOnboardingPayload.parse(VALID_ACTIVATION_PAYLOAD)

        val target = resolveDeviceOnboardingRuntimeTarget(
            payload = payload,
            configuredBaseUrl = null
        )

        assertSame(DeviceOnboardingRuntimeTarget.Missing, target)
    }

    @Test
    fun defaultRuntimeBaseUrlUsesConfiguredUrlBeforePersistedSessionUrl() {
        val baseUrl = resolveDefaultDeviceRuntimeBaseUrl(
            configuredBaseUrl = "https://build-config.example.test",
            persistedBaseUrl = "https://persisted.example.test"
        )

        assertEquals("https://build-config.example.test", baseUrl)
    }

    @Test
    fun defaultRuntimeBaseUrlFallsBackToPersistedSessionUrlAfterRestart() {
        val baseUrl = resolveDefaultDeviceRuntimeBaseUrl(
            configuredBaseUrl = null,
            persistedBaseUrl = "https://admin.ghostring.ru:18443"
        )

        assertEquals("https://admin.ghostring.ru:18443", baseUrl)
    }

    private companion object {
        const val VALID_ACTIVATION_PAYLOAD = "dac_0123456789abcdef0123456789abcdef.secret_PART-123"
    }
}
