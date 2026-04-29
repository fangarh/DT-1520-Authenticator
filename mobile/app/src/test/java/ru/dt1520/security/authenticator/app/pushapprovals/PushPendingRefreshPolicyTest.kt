package ru.dt1520.security.authenticator.app.pushapprovals

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class PushPendingRefreshPolicyTest {
    @Test
    fun resumeRequestsImmediateRefreshAndEnablesPolling() {
        val policy = PushPendingRefreshPolicy()

        val action = policy.onResume()

        assertEquals(PushPendingRefreshAction.RefreshNow, action)
        assertTrue(policy.isForegroundActive)
        assertEquals(PushPendingRefreshAction.RefreshNow, policy.onPollingTick())
    }

    @Test
    fun pauseStopsPollingAndTicksAreIgnored() {
        val policy = PushPendingRefreshPolicy()
        policy.onResume()

        val action = policy.onPauseOrStop()

        assertEquals(PushPendingRefreshAction.Stop, action)
        assertFalse(policy.isForegroundActive)
        assertEquals(PushPendingRefreshAction.Skip, policy.onPollingTick())
    }

    @Test
    fun pollingIntervalStaysInConservativeForegroundRange() {
        assertEquals(
            DEFAULT_PENDING_PUSH_REFRESH_INTERVAL_MILLIS,
            requireConservativePendingPushRefreshInterval(DEFAULT_PENDING_PUSH_REFRESH_INTERVAL_MILLIS)
        )
        assertThrows(IllegalArgumentException::class.java) {
            requireConservativePendingPushRefreshInterval(2_999L)
        }
        assertThrows(IllegalArgumentException::class.java) {
            requireConservativePendingPushRefreshInterval(5_001L)
        }
    }
}
