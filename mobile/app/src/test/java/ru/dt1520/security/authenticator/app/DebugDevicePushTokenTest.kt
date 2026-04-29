package ru.dt1520.security.authenticator.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DebugDevicePushTokenTest {
    @Test
    fun fromInstallationIdReturnsStableNonSecretToken() {
        val token = DebugDevicePushToken.fromInstallationId(" installation-1234 ")

        assertEquals("dt1520-debug-android-installation-1234", token)
    }

    @Test
    fun fromInstallationIdReturnsNullForBlankInstallation() {
        val token = DebugDevicePushToken.fromInstallationId("   ")

        assertNull(token)
    }
}
